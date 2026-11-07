using System;
using System.Runtime.InteropServices;

namespace Apps.XmlParser.ParamTab;

[UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
public delegate int delegateSetFeatureCallBack(int nFeatureType, IntPtr pFeatureName, IntPtr pSetValue, IntPtr pUser);
