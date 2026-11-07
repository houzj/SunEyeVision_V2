using System.Collections;
using System.Linq;
using VM.Core;
using VMControls.Interface;

namespace Apps.XmlParser.ParamTab;

public class CalibPointParam : IOutput
{
	public string DisplayType => "StaticParam";

	public string NameA { get; set; }

	public string ValueA => ToString();

	public PointR Point { get; set; }

	public override string ToString()
	{
		return $"(x={Point.X},y={Point.Y},r={Point.R})";
	}

	public void AddOutput(IVmModule vmModule)
	{
		if (vmModule is VmModule module)
		{
			AddCombineIO("POSTURE", NameA, module);
		}
	}

	private VmIO AddCombineIO(string type, string fname, VmModule module)
	{
		CombinRoot instance = CombinRoot.GetInstance();
		Combination combination = instance.Items.OfType<Combination>().FirstOrDefault((Combination p) => p.Style == type);
		combination.Name = fname;
		return AddIOFromCombination(combination, module, module, module.Outputs);
	}

	private VmIO AddIOFromCombination(object obj, VmModule owner, object parent, IList parentList, string suggestName = null)
	{
		if (obj is Combination combination)
		{
			string text = suggestName + combination.Name;
			DynamicVmIO dynamicVmIO = new DynamicVmIO
			{
				Name = text,
				Root = owner,
				Parent = parent,
				TypeName = combination.Style
			};
			parentList.Add(dynamicVmIO);
			foreach (object filter2 in combination.Filters)
			{
				AddIOFromCombination(filter2, owner, dynamicVmIO, dynamicVmIO.Childs, (text.StartsWith("%") && text.EndsWith("%")) ? text.Substring(1, text.Length - 2) : text);
			}
			return dynamicVmIO;
		}
		if (obj is Filter filter)
		{
			AddLeafIO(filter.ValueTypeA, owner, parent, parentList, suggestName + filter.NameA);
		}
		return null;
	}

	private VmIO AddLeafIO(string typeName, VmModule owner, object parent, IList parentList, string suggestName)
	{
		DynamicVmIO dynamicVmIO = new DynamicVmIO
		{
			Name = suggestName,
			Root = owner,
			Parent = parent,
			TypeName = typeName
		};
		parentList.Add(dynamicVmIO);
		return dynamicVmIO;
	}
}
