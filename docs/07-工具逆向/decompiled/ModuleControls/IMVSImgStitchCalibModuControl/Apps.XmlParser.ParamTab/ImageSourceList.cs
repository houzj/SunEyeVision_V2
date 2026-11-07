using VMControls.Interface;

namespace Apps.XmlParser.ParamTab;

public class ImageSourceList : NameValueItem
{
	public int ImageNumber;

	public BridgeItemBase ImageSourceContainer;

	private const int MVBSTITCH_MIN_IMG_NUM = 2;

	private const int MVBSTITCH_MAX_IMG_NUM = 100;

	private const string IMAGE_TYPE_NAME = "IMAGE";

	public bool IsHardStitch { get; set; }

	public int ImageNumberA
	{
		get
		{
			return ImageNumber;
		}
		set
		{
			if (ImageNumber != value)
			{
				ImageNumber = value;
				if (CustomVisibleA)
				{
					UpdateImageList();
				}
				if (IsHardStitch && GetTopBaseNode() is AlgorithmTabRoot algorithmTabRoot && algorithmTabRoot.FindBaseNodeByName("ImageRoiList") is AddList_ImageStitch addList_ImageStitch)
				{
					addList_ImageStitch.UpdateRoiList(value);
				}
			}
		}
	}

	public BridgeItemBase ImageSourceContainerA
	{
		get
		{
			return ImageSourceContainer;
		}
		set
		{
			ImageSourceContainer = value;
		}
	}

	public void UpdateImageList()
	{
		if (ImageNumber < 2 || ImageNumber > 100)
		{
			return;
		}
		if (ImageSourceContainerA.BridgeObjA is IMVSImgStitchCalibModuDynamicIO iMVSImgStitchCalibModuDynamicIO)
		{
			if (!CustomVisibleA)
			{
				int num = iMVSImgStitchCalibModuDynamicIO.ItemsA.Count - 1;
				for (int num2 = num; num2 >= 1; num2--)
				{
					if (iMVSImgStitchCalibModuDynamicIO.ItemsA[num2] is ImgStitchDynamicIO { IoDataSource: var ioDataSource })
					{
						ITypeName typeName = ioDataSource;
						if (ioDataSource != null && typeName.TypeName == "IMAGE")
						{
							iMVSImgStitchCalibModuDynamicIO.RemoveIO(iMVSImgStitchCalibModuDynamicIO.ItemsA[num2]);
						}
					}
				}
			}
			else if (iMVSImgStitchCalibModuDynamicIO.ItemsA.Count > ImageNumber)
			{
				int num3 = iMVSImgStitchCalibModuDynamicIO.ItemsA.Count - 1;
				for (int num4 = num3; num4 >= ImageNumber; num4--)
				{
					if (iMVSImgStitchCalibModuDynamicIO.ItemsA[num4] is ImgStitchDynamicIO { IoDataSource: var ioDataSource2 })
					{
						ITypeName typeName2 = ioDataSource2;
						if (ioDataSource2 != null && typeName2.TypeName == "IMAGE")
						{
							iMVSImgStitchCalibModuDynamicIO.RemoveIO(iMVSImgStitchCalibModuDynamicIO.ItemsA[num4]);
						}
					}
				}
			}
			else if (iMVSImgStitchCalibModuDynamicIO.ItemsA.Count < ImageNumber)
			{
				for (int i = iMVSImgStitchCalibModuDynamicIO.ItemsA.Count; i < ImageNumber; i++)
				{
					string name = "InputImage" + i;
					iMVSImgStitchCalibModuDynamicIO.AddIO(name, "IMAGE");
				}
			}
		}
		GetVmModule()?.ReFlashReadyState();
	}
}
