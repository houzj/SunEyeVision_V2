using System;
using System.Collections.Generic;
using System.Linq;

namespace SunEyeVision.Plugin.SDK.Models;
/// <summary>Group IO identity rules shared by configuration and execution validation.</summary>
public static class SubroutineIoRules
{
    public const int MaximumItems=1024;
    public const int MaximumNameLength=31;
    private static readonly char[] InvalidCharacters={'?',';','.','/','\\','`','{','}','[',']','<','>','(',')','%'};
    public static bool IsValidName(string? name,int maximumLength=MaximumNameLength)=>!string.IsNullOrWhiteSpace(name)&&name.Length<=maximumLength&&name.IndexOfAny(InvalidCharacters)<0;
    public static string SuggestName(string? value,string fallback)
    {
        var name=new string((value??"").Select(c=>InvalidCharacters.Contains(c)?'_':c).ToArray()).Trim();
        if(string.IsNullOrWhiteSpace(name))name=fallback;
        return name[..Math.Min(name.Length,MaximumNameLength)];
    }
    public static bool HasConsistentType(string typeName,Type? actual)
    {
        var declared=SdkTypeVocabulary.ToSystemType(typeName);if(declared is null)return false;
        var vocabulary=SdkTypeVocabulary.ToTypeVocabulary(actual??declared);
        return vocabulary is not null&&vocabulary==SdkTypeVocabulary.ToTypeVocabulary(declared);
    }
    public static string UniqueName(string prefix,IEnumerable<string> existing)
    {
        if(!IsValidName(prefix))throw new ArgumentException("IO名称前缀无效。",nameof(prefix));
        var names=existing.ToHashSet(StringComparer.Ordinal);if(!names.Contains(prefix))return prefix;
        for(var index=0;;index++)
        {
            var suffix=index.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var candidate=prefix[..Math.Min(prefix.Length,MaximumNameLength-suffix.Length)]+suffix;
            if(!names.Contains(candidate))return candidate;
        }
    }
}
