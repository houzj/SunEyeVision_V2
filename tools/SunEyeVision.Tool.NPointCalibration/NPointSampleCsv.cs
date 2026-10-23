using System.Globalization;

namespace SunEyeVision.Tool.NPointCalibration;

/// <summary>Versioned manual point exchange. Imported rows never claim acquisition evidence.</summary>
public static class NPointSampleCsv
{
    public const string VersionHeader="Native9Plus3V3;PhysicalUnit=mm;AngleUnit=deg;Image=ColumnRow";
    public const string Columns="Id,Kind,Enabled,ImageX,ImageY,AxisX,AxisY,AxisAngle";
    public static string[] Export(IEnumerable<NPointSample> rows)
    {
        string F(double x)=>x.ToString("R",CultureInfo.InvariantCulture);
        return new[]{VersionHeader,Columns}.Concat(rows.Where(s=>s.Source!="Placeholder").Select(s=>
            string.Join(",",s.Id,s.Kind,s.Enabled,F(s.ImageX),F(s.ImageY),F(s.AxisX),F(s.AxisY),F(s.AxisAngle)))).ToArray();
    }
    public static NPointSample[] Parse(string[] lines,int translationCount,int rotationCount)
    {
        if(lines.Length<2 || lines[0].Trim()!=VersionHeader || lines[1].Trim()!=Columns)
            throw new FormatException("CSV版本、单位或列名不匹配；不自动兼容旧点表。");
        double D(string x)=>double.Parse(x,CultureInfo.InvariantCulture);
        var rows=lines.Skip(2).Where(l=>!string.IsNullOrWhiteSpace(l)).Select(line=>
        {
            var v=line.Split(',');if(v.Length!=8)throw new FormatException("CSV列数错误。");
            return new NPointSample{Id=int.Parse(v[0],CultureInfo.InvariantCulture),Kind=Enum.Parse<NPointSampleKind>(v[1]),
                Enabled=bool.Parse(v[2]),ImageX=D(v[3]),ImageY=D(v[4]),AxisX=D(v[5]),AxisY=D(v[6]),AxisAngle=D(v[7]),Source="ManualImported"};
        }).ToArray();
        if(rows.Select(s=>s.Id).Distinct().Count()!=rows.Length || rows.Any(s=>s.Id<0 || s.Id>=translationCount+rotationCount ||
            !Enum.IsDefined(s.Kind) || s.Kind!=(s.Id<translationCount?NPointSampleKind.Translation:NPointSampleKind.Rotation) ||
            new[]{s.ImageX,s.ImageY,s.AxisX,s.AxisY,s.AxisAngle}.Any(v=>!double.IsFinite(v))))
            throw new FormatException("CSV有重复/越界点号、类别错误或非有限数值；原点表未替换。");
        return rows;
    }
}
