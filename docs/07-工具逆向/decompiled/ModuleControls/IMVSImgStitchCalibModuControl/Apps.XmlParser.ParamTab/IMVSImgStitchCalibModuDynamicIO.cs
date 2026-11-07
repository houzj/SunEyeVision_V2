using System.Collections.ObjectModel;
using Apps.Interface;
using VM.Core;

namespace Apps.XmlParser.ParamTab;

public class IMVSImgStitchCalibModuDynamicIO : DynamicIOContainer<ImgStitchDynamicIO>
{
	private const int MAX_IMAGE_COUNT = 100;

	private const string IMAGE_TYPE_NAME = "IMAGE";

	private ObservableCollection<object> _ItemsA = null;

	public override int MaxItemCount => 100;

	public override ObservableCollection<object> ItemsA
	{
		get
		{
			if (_ItemsA == null)
			{
				_ItemsA = new ObservableCollection<object>();
				if (DataSource != null)
				{
					int num = 0;
					foreach (VmIO targetVMIO in TargetVMIOs)
					{
						if (targetVMIO.TypeName == "IMAGE")
						{
							ImgStitchDynamicIO imgStitchDynamicIO = new ImgStitchDynamicIO();
							imgStitchDynamicIO.IoDataSource = targetVMIO;
							if (targetVMIO.CustomVisibleA)
							{
								num++;
								imgStitchDynamicIO.IndexA = num.ToString();
							}
							imgStitchDynamicIO.SetParentNode(this);
							_ItemsA.Add(imgStitchDynamicIO);
						}
					}
					UpdateCanDelete();
				}
			}
			return _ItemsA;
		}
		set
		{
			if (_ItemsA != value)
			{
				_ItemsA = value;
				OnPropertyChanged("ItemsA");
			}
		}
	}

	public IMVSImgStitchCalibModuDynamicIO(string name, IOwner dataSorce)
		: base(name, dataSorce)
	{
		SetParentNode(AlgorithmItems);
		IsAddForceIO = true;
	}
}
