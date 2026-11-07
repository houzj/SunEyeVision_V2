using System.ComponentModel;
using System.Xml;
using Hik.Xml;
using VM.PlatformSDKCS;
using VMControls.Interface;

namespace Apps.XmlParser.ParamTab;

public class LocalImageList : NameValueItem
{
	private AlgorithmTabRoot imageAlgorithmTabRoot;

	private bool isShowLocalImageList;

	public AlgorithmTabRoot ImageAlgorithmTabRoot
	{
		get
		{
			if (imageAlgorithmTabRoot == null)
			{
				imageAlgorithmTabRoot = GetTopBaseNode() as AlgorithmTabRoot;
			}
			return imageAlgorithmTabRoot;
		}
	}

	public IVmModule DecorateSource => (GetTopBaseNode() as AlgorithmTabRoot).Owner as IVmModule;

	public bool IsShowLocalImageList
	{
		get
		{
			return isShowLocalImageList;
		}
		set
		{
			if (isShowLocalImageList != value)
			{
				isShowLocalImageList = value;
				OnPropertyChanged("IsShowLocalImageList");
			}
		}
	}

	public new event PropertyChangedEventHandler PropertyChanged;

	public new virtual void OnPropertyChanged(string name)
	{
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
	}

	public override BaseNode Init(XmlNode node)
	{
		base.Init(node);
		if (CVmObjectBase.GetIsSDKMode())
		{
			CustomVisible = true;
		}
		return this;
	}
}
