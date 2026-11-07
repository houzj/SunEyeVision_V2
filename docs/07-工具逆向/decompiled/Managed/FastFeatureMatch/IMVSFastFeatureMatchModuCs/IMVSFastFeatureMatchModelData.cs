using System.Collections.Generic;
using Hik.Xml;

namespace IMVSFastFeatureMatchModuCs;

/// @cond
/// <summary>
/// 模型缓存数据 用于撤销重做
/// </summary>
public class IMVSFastFeatureMatchModelData : BaseNode
{
	public List<object> Models = new List<object>();

	public List<object> UIDatas = new List<object>();
}
