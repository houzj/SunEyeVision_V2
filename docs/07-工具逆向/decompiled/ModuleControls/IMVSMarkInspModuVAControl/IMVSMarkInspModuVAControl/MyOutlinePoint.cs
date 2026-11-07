using Apps.Data.ShapeDefine;
using VMControls.RenderInterface;

namespace IMVSMarkInspModuVAControl;

public class MyOutlinePoint : Shape, IOutlinePoint, IShape, IShapeBase
{
	public byte[] OutLineInfo;

	private string ngColor;

	public string NgColorA
	{
		get
		{
			return ngColor;
		}
		set
		{
			if (ngColor != value)
			{
				ngColor = value;
				OnPropertyChanged("NgColorA");
			}
		}
	}

	public byte[] OutlineData => OutLineInfo;

	public string NgColor => NgColorA;
}
