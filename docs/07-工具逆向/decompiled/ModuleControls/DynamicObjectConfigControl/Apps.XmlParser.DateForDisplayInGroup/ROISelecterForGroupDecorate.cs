using System;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Apps.BaseData;
using Apps.Data;
using Apps.XmlParser.ItemInterface;
using Apps.XmlParser.ParamTab;
using Mvvm.Commands;
using VM.Core;
using VMControls.Interface;
using VMControls.RenderInterface;

namespace Apps.XmlParser.DateForDisplayInGroup;

internal class ROISelecterForGroupDecorate : NotifyPropertyChangedBase, IDecorate
{
	private ROISelecter _DecorateSource;

	private int _imageWidth;

	private int _imageHeight;

	private ImageData imageSource;

	private ICommand _RoiSelectedCommand = null;

	private ICommand _ImageSizeChangedCommand = null;

	private ICommand _RoiCopyCommand = null;

	private ICommand _RoiDelCommand = null;

	private ICommand _RoiPasteCommand = null;

	private ICommand _RoiVisibleChangedCommand = null;

	private IVmModule _ModuleSource;

	public string[] SupportDisplayForGroupData { get; } = new string[1] { "ROISelecter_GroupControl" };

	public string DisplayType => SupportDisplayForGroupData[0];

	public ROISelecter DecorateSource
	{
		get
		{
			return _DecorateSource;
		}
		set
		{
			if (value != _DecorateSource)
			{
				_DecorateSource = value;
				InitSource(_DecorateSource);
			}
		}
	}

	object IDecorate.DecorateSource
	{
		get
		{
			return DecorateSource;
		}
		set
		{
			DecorateSource = value as ROISelecter;
		}
	}

	public int ImageWidth
	{
		get
		{
			return _imageWidth;
		}
		set
		{
			if (_imageWidth != value)
			{
				_imageWidth = value;
				OnPropertyChanged("ImageWidth");
			}
		}
	}

	public int ImageHeight
	{
		get
		{
			return _imageHeight;
		}
		set
		{
			if (_imageHeight != value)
			{
				_imageHeight = value;
				OnPropertyChanged("ImageHeight");
			}
		}
	}

	public ImageData ImageSource
	{
		get
		{
			if (imageSource == null)
			{
				GetImage();
			}
			return imageSource;
		}
		set
		{
			if (ImageSource != value)
			{
				imageSource = value;
				OnPropertyChanged("ImageSource");
				OnPropertyChanged("ImageWidth");
				OnPropertyChanged("ImageHeight");
			}
		}
	}

	public ObservableRangeCollection<IROI> ROIList => DecorateSource.ROIList;

	public ICommand RoiSelectedCommand => _RoiSelectedCommand ?? (_RoiSelectedCommand = (ICommand)new DelegateCommand<RoiWithTagEventArgs>((Action<RoiWithTagEventArgs>)RoiSelectedCommandExecute));

	public ICommand ImageSizeChangedCommand => _ImageSizeChangedCommand ?? (_ImageSizeChangedCommand = (ICommand)new DelegateCommand<object>((Action<object>)ImageSizeChangedCommandExecute));

	public ICommand RoiCopyCommand => _RoiCopyCommand ?? (_RoiCopyCommand = (ICommand)new DelegateCommand<RoiWithTagEventArgs>((Action<RoiWithTagEventArgs>)RoiCopyCommandExecute, (Func<RoiWithTagEventArgs, bool>)CanRoiCopyCommandExecute, false));

	public ICommand RoiDelCommand => _RoiDelCommand ?? (_RoiDelCommand = (ICommand)new DelegateCommand<RoiWithTagEventArgs>((Action<RoiWithTagEventArgs>)RoiDelCommandExecute, (Func<RoiWithTagEventArgs, bool>)CanRoiDelCommandExecute, false));

	public ICommand RoiPasteCommand => _RoiPasteCommand ?? (_RoiPasteCommand = (ICommand)new DelegateCommand<RoiWithTagEventArgs>((Action<RoiWithTagEventArgs>)RoiPasteCommandExecute));

	public ICommand RoiVisibleChangedCommand => _RoiVisibleChangedCommand ?? (_RoiVisibleChangedCommand = (ICommand)new DelegateCommand<object>((Action<object>)RoiVisibleChangedCommandExecute));

	public IVmModule ModuleSource
	{
		get
		{
			if (_ModuleSource == null)
			{
				VmModuleSet obj = (DecorateSource.GetTopBaseNode() as AlgorithmTabRoot)?.Owner as VmModuleSet;
				_ModuleSource = ((obj != null) ? obj.GetFirstParamRelationModule(DecorateSource.Name) : null);
			}
			return _ModuleSource;
		}
	}

	private void InitSource(ROISelecter decorateSource)
	{
		if (decorateSource != null)
		{
			decorateSource.IsStateForDrawROI = false;
			decorateSource.SelectNone();
		}
	}

	private void GetImage()
	{
		IVmModule moduleSource = ModuleSource;
		if (moduleSource == null)
		{
			return;
		}
		foreach (VmIO item in from p in moduleSource.Inputs.OfType<VmIO>()
			where p.TypeName == "IMAGE"
			select p)
		{
			if (item.RelateIO != null)
			{
				object combinationValueByType = item.RelateIO.GetCombinationValueByType(uint.MaxValue);
				imageSource = combinationValueByType as ImageData;
				break;
			}
		}
	}

	private void RoiSelectedCommandExecute(RoiWithTagEventArgs args)
	{
		object tag = args.Tag;
		IROI roi = args.Roi;
		((IROISelecter)DecorateSource)?.DoRoiSelect(roi);
	}

	private void ImageSizeChangedCommandExecute(object data)
	{
		Size size = (Size)data;
		ImageWidth = (int)size.Width;
		ImageHeight = (int)size.Height;
	}

	private bool CanRoiCopyCommandExecute(RoiWithTagEventArgs args)
	{
		object tag = args.Tag;
		IROI roi = args.Roi;
		return ((IROISelecter)DecorateSource)?.CanCopy(roi) ?? false;
	}

	private void RoiCopyCommandExecute(RoiWithTagEventArgs args)
	{
	}

	private bool CanRoiDelCommandExecute(RoiWithTagEventArgs args)
	{
		object tag = args.Tag;
		IROI roi = args.Roi;
		return ((IROISelecter)DecorateSource)?.CanDeleted(roi) ?? false;
	}

	private void RoiDelCommandExecute(RoiWithTagEventArgs args)
	{
		object tag = args.Tag;
		IROI roi = args.Roi;
		((IROISelecter)DecorateSource)?.DoRoiDeleted(roi);
	}

	private void RoiPasteCommandExecute(RoiWithTagEventArgs args)
	{
		object tag = args.Tag;
		IROI roi = args.Roi;
		((IROISelecter)DecorateSource)?.DoRoiPaste(roi);
	}

	private void RoiVisibleChangedCommandExecute(object args)
	{
		bool flag = (bool)args;
		IROISelecter decorateSource = DecorateSource;
		if (decorateSource != null)
		{
			if (!flag)
			{
				decorateSource.DoRoiSelect(null);
			}
			else
			{
				decorateSource.DoRoiSelect(ROISelecter.OldROI);
			}
		}
	}
}
