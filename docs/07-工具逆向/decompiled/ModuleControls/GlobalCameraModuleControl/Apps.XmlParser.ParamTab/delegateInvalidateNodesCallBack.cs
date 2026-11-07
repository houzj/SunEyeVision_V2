using System;
using System.Runtime.InteropServices;

namespace Apps.XmlParser.ParamTab;

[UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi)]
public delegate int delegateInvalidateNodesCallBack(IntPtr pUser);
