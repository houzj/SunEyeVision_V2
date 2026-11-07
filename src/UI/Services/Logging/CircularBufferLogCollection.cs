using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using SunEyeVision.Foundation.Logging;

namespace SunEyeVision.UI.Services.Logging
{
    /// <summary>
    /// 高性能循环缓冲区日志集合 - O(1) 添加和移除操作
    /// </summary>
    /// <remarks>
    /// 性能优化点：
    /// - 循环缓冲区结构：添加 O(1)，移除 O(1)
    /// - 批量添加：单次 NotifyCollectionChanged 事件
    /// - 增量统计：避免遍历计算
    /// </remarks>
    public class CircularBufferLogCollection : IList<LogEntry>, INotifyCollectionChanged, INotifyPropertyChanged
    {
        #region 常量

        /// <summary>
        /// 默认容量
        /// </summary>
        public const int DefaultCapacity = 5000;

        #endregion

        #region 私有字段

        private readonly LogEntry[] _buffer;
        private int _head;      // 指向最旧的元素
        private int _tail;      // 指向下一个空位
        private int _count;
        private readonly int _capacity;
        private bool _isBatchUpdate;
        private readonly Dispatcher _dispatcher;
        private int _removedSinceLastReset;

        // 增量统计
        private int _errorCount;
        private int _warningCount;
        private int _successCount;

        // 总入队数量（不受容量限制影响）
        private long _totalEnqueued;

        #endregion

        #region 属性

        /// <summary>
        /// 当前日志数量
        /// </summary>
        public int Count => _count;

        /// <summary>
        /// 容量
        /// </summary>
        public int Capacity => _capacity;

        /// <summary>
        /// 是否只读
        /// </summary>
        public bool IsReadOnly => true;

        /// <summary>
        /// 错误日志数量（增量维护，O(1)访问）
        /// </summary>
        public int ErrorCount => _errorCount;

        /// <summary>
        /// 警告日志数量
        /// </summary>
        public int WarningCount => _warningCount;

        /// <summary>
        /// 成功日志数量
        /// </summary>
        public int SuccessCount => _successCount;

        /// <summary>
        /// 总入队数量（不受容量限制影响）
        /// 用于显示真实写入量，即使缓冲区满也持续增长
        /// </summary>
        public long TotalEnqueued => _totalEnqueued;

        #endregion

        #region 索引器

        public LogEntry this[int index]
        {
            get
            {
                if (index < 0 || index >= _count)
                    throw new ArgumentOutOfRangeException(nameof(index));

                var actualIndex = (_head + index) % _capacity;
                return _buffer[actualIndex];
            }
            set => throw new NotSupportedException("循环缓冲区不支持设置操作");
        }

        #endregion

        #region 构造函数

        public CircularBufferLogCollection(int capacity = DefaultCapacity) : this(capacity, System.Windows.Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher)
        {
        }

        public CircularBufferLogCollection(int capacity, Dispatcher dispatcher)
        {
            if (capacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(capacity));
            if (dispatcher == null)
                throw new ArgumentNullException(nameof(dispatcher));

            _capacity = capacity;
            _buffer = new LogEntry[capacity];
            _head = 0;
            _tail = 0;
            _count = 0;
            _dispatcher = dispatcher;
            _removedSinceLastReset = 0;
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 添加单个日志条目 - O(1)
        /// </summary>
        public void Add(LogEntry item)
        {
            if (item == null)
                throw new ArgumentNullException(nameof(item));

            // 总入队计数始终递增
            _totalEnqueued++;

            if (_count < _capacity)
            {
                // 缓冲区未满，直接添加
                _buffer[_tail] = item;
                _tail = (_tail + 1) % _capacity;
                _count++;
            }
            else
            {
                // 缓冲区已满，覆盖最旧的
                var oldItem = _buffer[_head];
                DecrementStatistics(oldItem);

                _buffer[_tail] = item;
                _head = (_head + 1) % _capacity;
                _tail = (_tail + 1) % _capacity;
            }

            IncrementStatistics(item);

            if (!_isBatchUpdate)
            {
                OnCollectionChanged(new NotifyCollectionChangedEventArgs(
                    NotifyCollectionChangedAction.Add, item, _count - 1));
                OnPropertyChanged(nameof(Count));
                OnPropertyChanged(nameof(TotalEnqueued));
            }
        }

        /// <summary>
        /// 批量添加日志条目 - 单次事件通知
        /// </summary>
        public void AddRange(IReadOnlyList<LogEntry> items)
        {
            if (items == null || items.Count == 0)
                return;

            _isBatchUpdate = true;
            var startIndex = _count;

            // 计算需要移除的旧日志数量
            int removedCount = 0;
            var oldItems = new List<LogEntry>();

            if (_count + items.Count > _capacity)
            {
                removedCount = _count + items.Count - _capacity;
                for (int i = 0; i < removedCount && _count > 0; i++)
                {
                    var oldItem = _buffer[_head];
                    oldItems.Add(oldItem);
                    DecrementStatistics(oldItem);
                    _head = (_head + 1) % _capacity;
                    _count--;
                }
            }

            // 添加新日志
            foreach (var item in items)
            {
                _buffer[_tail] = item;
                _tail = (_tail + 1) % _capacity;
                _count++;
                _totalEnqueued++;  // 总入队计数始终递增
                IncrementStatistics(item);
            }

            _isBatchUpdate = false;

            // 单次事件通知
            if (removedCount > 0)
            {
                // 循环缓冲区满时触发Reset，让WPF重新绑定
                // 使用延迟触发，等待UI绑定完成
                _removedSinceLastReset += removedCount;
                ScheduleReset();
            }
            else
            {
                OnCollectionChanged(new NotifyCollectionChangedEventArgs(
                    NotifyCollectionChangedAction.Add,
                    new List<LogEntry>(items),
                    startIndex));
            }

            OnPropertyChanged(nameof(Count));
            OnPropertyChanged(nameof(TotalEnqueued));
        }

        /// <summary>
        /// 清空集合 - O(1)
        /// </summary>
        public void Clear()
        {
            if (_count == 0)
                return;

            var oldItems = new List<LogEntry>();
            for (int i = 0; i < _count; i++)
            {
                oldItems.Add(_buffer[(_head + i) % _capacity]);
            }

            _head = 0;
            _tail = 0;
            _count = 0;
            _errorCount = 0;
            _warningCount = 0;
            _successCount = 0;

            // 注意：不清空 _totalEnqueued，保留总入队计数用于统计

            OnCollectionChanged(new NotifyCollectionChangedEventArgs(
                NotifyCollectionChangedAction.Remove, oldItems, 0));
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(
                NotifyCollectionChangedAction.Reset));
            OnPropertyChanged(nameof(Count));
            OnPropertyChanged(nameof(ErrorCount));
            OnPropertyChanged(nameof(WarningCount));
            OnPropertyChanged(nameof(SuccessCount));
        }

        /// <summary>
        /// 查找元素索引
        /// </summary>
        public int IndexOf(LogEntry item)
        {
            for (int i = 0; i < _count; i++)
            {
                if (Equals(_buffer[(_head + i) % _capacity], item))
                    return i;
            }
            return -1;
        }

        /// <summary>
        /// 是否包含元素
        /// </summary>
        public bool Contains(LogEntry item)
        {
            return IndexOf(item) >= 0;
        }

        /// <summary>
        /// 复制到数组
        /// </summary>
        public void CopyTo(LogEntry[] array, int arrayIndex)
        {
            if (array == null)
                throw new ArgumentNullException(nameof(array));
            if (arrayIndex < 0 || arrayIndex + _count > array.Length)
                throw new ArgumentOutOfRangeException(nameof(arrayIndex));

            for (int i = 0; i < _count; i++)
            {
                array[arrayIndex + i] = _buffer[(_head + i) % _capacity];
            }
        }

        /// <summary>
        /// 获取枚举器
        /// </summary>
        public IEnumerator<LogEntry> GetEnumerator()
        {
            for (int i = 0; i < _count; i++)
            {
                yield return _buffer[(_head + i) % _capacity];
            }
        }

        #endregion

        #region 不支持的操作

        public void Insert(int index, LogEntry item) => throw new NotSupportedException();
        public bool Remove(LogEntry item) => throw new NotSupportedException("请使用 Clear() 或让容量自动管理");
        public void RemoveAt(int index) => throw new NotSupportedException("请使用 Clear() 或让容量自动管理");

        #endregion

        #region 私有方法

        /// <summary>
        /// 调度Reset事件触发（延迟触发以等待UI绑定完成）
        /// </summary>
        /// <remarks>
        /// 修复说明（2026-03-16）：修复Dispatcher.BeginInvoke参数顺序错误
        /// 原bug：_dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, () => { ... });
        /// 修复：_dispatcher.BeginInvoke(new Action(() => { ... }), DispatcherPriority.ContextIdle);
        /// </remarks>
        private void ScheduleReset()
        {
            var removedCount = Interlocked.Exchange(ref _removedSinceLastReset, 0);

            if (removedCount > 0)
            {
                // ✅ 修复：正确的参数顺序
                _dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        OnCollectionChanged(new NotifyCollectionChangedEventArgs(
                            NotifyCollectionChangedAction.Reset));
                    }
                    catch (Exception)
                    {
                        // 忽略可能的集合异常（如UI正在销毁）
                    }
                }), DispatcherPriority.ContextIdle);
            }
        }

        private void IncrementStatistics(LogEntry entry)
        {
            switch (entry.Level)
            {
                case LogLevel.Error:
                    _errorCount++;
                    break;
                case LogLevel.Warning:
                    _warningCount++;
                    break;
                case LogLevel.Success:
                    _successCount++;
                    break;
            }
        }

        private void DecrementStatistics(LogEntry entry)
        {
            switch (entry.Level)
            {
                case LogLevel.Error:
                    _errorCount--;
                    break;
                case LogLevel.Warning:
                    _warningCount--;
                    break;
                case LogLevel.Success:
                    _successCount--;
                    break;
            }
        }

        #endregion

        #region INotifyCollectionChanged

        public event NotifyCollectionChangedEventHandler? CollectionChanged;

        protected virtual void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            CollectionChanged?.Invoke(this, e);
        }

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion

        #region IEnumerable

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        #endregion
    }
}
