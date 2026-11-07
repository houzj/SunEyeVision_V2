using System;
using System.Collections.Generic;
using System.Linq;

namespace App.XmlParser;

public class ExpressBuilder
{
	private static string FunMatchPattern = string.Empty;

	private readonly Stack<IStackObject> _operatorStack;

	private readonly List<IStackObject> _rpnList;

	internal List<IStackObject> RpnList => _rpnList;

	public string RPNExpression { get; private set; }

	public ExpressBuilder()
	{
		if (FunMatchPattern == string.Empty)
		{
			IEnumerable<string> source = from p in DefaultOperators.Operators
				where p.Value.IsFun
				select Word.GetEscapeString(p.Key);
			FunMatchPattern = string.Join("|", source.ToArray());
		}
		_operatorStack = new Stack<IStackObject>();
		_rpnList = new List<IStackObject>();
	}

	private static void GetFunctionParamGroup(List<Word> expression)
	{
		IEnumerable<Word> enumerable = expression.Where((Word p) => p.Type == "fun");
		foreach (Word item in enumerable)
		{
			GetFunctionEndIndex(expression, item);
		}
	}

	private static void GetFunctionEndIndex(List<Word> expression, Word fun)
	{
		List<string> list = new List<string>();
		int num = -1;
		List<Word> list2 = new List<Word>();
		List<Word> list3 = expression.Where((Word p) => p.StartIndex > fun.StartIndex).ToList();
		List<Word> list4 = list3.Where((Word p) => p.Type == "groupoperator").ToList();
		if (!list4.Any() || list4[0].ToString() != "(" || list4[0].StartIndex > fun.StartIndex + fun.Length)
		{
			throw new Exception($"函数{fun}未找到开始'(',位置:{fun.StartIndex + fun.Length}");
		}
		int index = 0;
		list4[0].IsFunGroup = true;
		foreach (Word item in list4)
		{
			index = item.StartIndex;
			if (list.Count == 0)
			{
				list.Add(item.ToString());
				num++;
				continue;
			}
			if (item.ToString() == ",")
			{
				if ((list[0] == "(" && num == 0) || (item.ToString() == "," && list[0] == ")" && num == -1))
				{
					list2.Add(item);
				}
				continue;
			}
			string text = list[0];
			if (text == item.ToString())
			{
				if (text == "(")
				{
					num--;
				}
				if (text == ")")
				{
					num++;
				}
			}
			list.RemoveAt(0);
			list.Add(item.ToString());
			if (num != 0)
			{
				continue;
			}
			item.IsFunGroup = true;
			break;
		}
		if (num != 0)
		{
			throw new Exception($"函数{fun}未找到结束')',位置:{index}");
		}
		List<int> list5 = (from p in list2
			where p.StartIndex < index
			select p.StartIndex).ToList();
		list5.Add(list3[0].StartIndex);
		list5.Add(index);
		list5 = list5.OrderBy((int p) => p).ToList();
		List<Word> list6 = list3.Where((Word p) => p.StartIndex <= index).ToList();
		int num2 = list5.Count - 1;
		if (num2 != DefaultOperators.Operators[fun.ToString()].ParamCount)
		{
			throw new Exception($"函数{fun}要求的参数与输入不一致，要求{DefaultOperators.Operators[fun.ToString()].ParamCount}个，实际{num2}个");
		}
		CheckParams(list6, list5, fun);
	}

	private static void CheckParams(List<Word> group, List<int> separatorIndex, Word fun)
	{
		for (int i = 0; i < separatorIndex.Count - 1; i++)
		{
			int from = separatorIndex[i];
			int to = separatorIndex[i + 1];
			IEnumerable<Word> source = group.Where((Word p) => p.StartIndex < to && p.StartIndex > from && p.Type != "groupoperator");
			if (!source.Any())
			{
				throw new Exception($"函数{fun}参数错误，无参数值或无法推导出有效操作数！位置:{from}~{to}");
			}
		}
	}

	private void Regulate(IOperator op)
	{
		while (true)
		{
			if (_operatorStack.Count == 0)
			{
				_operatorStack.Push(op);
				return;
			}
			if (!(_operatorStack.Peek() is IOperator obj))
			{
				_operatorStack.Push(op);
				return;
			}
			if (op.Priority > obj.Priority)
			{
				break;
			}
			_rpnList.Add(_operatorStack.Pop());
		}
		_operatorStack.Push(op);
	}

	private void RegulateEXP(List<Word> words)
	{
		List<Word> list = words.Where((Word p) => p.ToString() == "-" && p.Type == "operator").ToList();
		foreach (Word item in list)
		{
			int index = words.IndexOf(item) + 1;
			words.InsertWord(index, new Word("*")
			{
				Type = "operator"
			});
			words.InsertWord(index, new Word("1")
			{
				Type = "operand"
			});
		}
	}

	public bool Parse(string expression, out string msg)
	{
		msg = string.Empty;
		_operatorStack.Clear();
		_rpnList.Clear();
		RPNExpression = string.Empty;
		try
		{
			if (string.IsNullOrEmpty(expression))
			{
				throw new Exception("表达式不能为空！");
			}
			expression = expression.Trim().ToLower().Replace(" ", "");
			if (string.IsNullOrEmpty(expression))
			{
				throw new Exception("表达式不能为空！");
			}
			List<Word> list = Word.ExpressionToWords(expression);
			GetFunctionParamGroup(list);
			if (list.Count == 0)
			{
				throw new FormatException("未解析到支持的字符！");
			}
			Word word = list.Last();
			if (word.StartIndex + word.Length < expression.Length)
			{
				throw new FormatException($"'{word}'附近存在不被支持的字符'{expression.Substring(word.StartIndex + word.Length)}',位置:{word.StartIndex + word.Length}");
			}
			RegulateEXP(list);
			int num = 0;
			foreach (Word item in list)
			{
				Word preWord = list.GetPreWord(item);
				if (item.StartIndex != num)
				{
					throw new FormatException($"'{item}'附近存在不被支持的字符'{expression.Substring((preWord != null) ? (preWord.StartIndex + preWord.Length) : 0, item.StartIndex - ((preWord != null) ? (preWord.StartIndex + 1) : 0))}',位置:{((preWord != null) ? (preWord.StartIndex + preWord.Length) : 0)}~{item.StartIndex - 1}");
				}
				num += item.Length;
				switch (item.Type)
				{
				case "fun":
				case "operator":
				{
					IOperator obj = DefaultOperators.Operators[item.ToString()];
					if (obj.Symbol == "-" && (preWord == null || (preWord.Type != "operand" && preWord.ToString() != ")")))
					{
						obj = DefaultOperators.Operators["negate"];
					}
					Regulate(obj);
					break;
				}
				case "groupoperator":
					switch (item.ToString())
					{
					case "(":
					{
						for (int num3 = ((!item.IsFunGroup) ? 1 : 2); num3 > 0; num3--)
						{
							_operatorStack.Push(new GroupOperator
							{
								Symbol = "("
							});
						}
						break;
					}
					case ")":
					{
						for (int num2 = ((!item.IsFunGroup) ? 1 : 2); num2 > 0; num2--)
						{
							if (_operatorStack.Count == 0)
							{
								throw new Exception("表达式异常关闭！未找到'(");
							}
							bool flag2 = false;
							IStackObject stackObject2 = _operatorStack.Pop();
							while (true)
							{
								if (stackObject2.Type == StackObjectType.GroupOperator)
								{
									flag2 = true;
									break;
								}
								_rpnList.Add(stackObject2);
								if (_operatorStack.Count == 0)
								{
									break;
								}
								stackObject2 = _operatorStack.Pop();
							}
							if (!flag2)
							{
								throw new Exception("表达式异常关闭！未找到'(");
							}
						}
						break;
					}
					case ",":
					{
						if (_operatorStack.Count == 0)
						{
							throw new Exception("表达式异常关闭！未找到'(");
						}
						bool flag = false;
						IStackObject stackObject = _operatorStack.Pop();
						while (true)
						{
							if (stackObject.Type == StackObjectType.GroupOperator)
							{
								flag = true;
								break;
							}
							_rpnList.Add(stackObject);
							if (_operatorStack.Count == 0)
							{
								break;
							}
							stackObject = _operatorStack.Pop();
						}
						if (!flag)
						{
							throw new Exception("表达式异常关闭！未找到'(");
						}
						_operatorStack.Push(new GroupOperator
						{
							Symbol = "("
						});
						break;
					}
					}
					break;
				case "operand":
					_rpnList.Add(new Operand
					{
						Symbol = item.ToString(),
						Value = double.Parse(item.ToString())
					});
					break;
				}
			}
			while (_operatorStack.Count > 0)
			{
				IStackObject stackObject3 = _operatorStack.Pop();
				if (stackObject3.Type == StackObjectType.GroupOperator)
				{
					throw new Exception($"意外的操作符'{stackObject3.Symbol}'");
				}
				_rpnList.Add(stackObject3);
			}
			RPNExpression = string.Join(" ", _rpnList.Select((IStackObject p) => p.Symbol).ToArray());
			_rpnList.Insert(0, new Operand
			{
				Symbol = "#"
			});
			return true;
		}
		catch (Exception ex)
		{
			msg = ex.Message;
			return false;
		}
	}
}
