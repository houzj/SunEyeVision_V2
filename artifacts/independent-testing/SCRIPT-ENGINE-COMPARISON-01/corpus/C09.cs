using System;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using BenchCommon;
public static class Probe { public static void Run() {
int[] values = new int[] { 1, 2 }; values.Select(x => x + 1);
} }