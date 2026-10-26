"""Import the three original input-control libraries without inventing implementations.

This is a source migration, not a build or equivalence test. Unavailable dependency
and export-body cases retain their actual source as .pending, outside WPF's implicit
Compile/Page items. No shared project or Generic dictionary is modified.
"""
from __future__ import annotations
import argparse
import hashlib
import json
import re
from pathlib import Path
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]
PREFIX = "SunEyeVision.Plugin.Wpf.Native.Input."
LIBRARIES = ("VMControls.WPF", "VMControls.WPF.Release", "VM.PcUI")
XAML_NS = "http://schemas.microsoft.com/winfx/2006/xaml"
EXCLUDED = re.compile(r"Numeric|Numberic|Number|Slider|Digit|Decimal|RangeRule|SymbolType", re.I)
IMAGE_EXT = {".png", ".jpg", ".jpeg", ".gif", ".bmp", ".ico", ".cur"}
NUMERIC_TYPES = {
    **{f"HikPcUI.ListView.{name}": f"SunEyeVision.Plugin.Wpf.Native.Numeric.HikPcUI.ListView.{name}" for name in (
        "NumericControl", "NumbericTextBox", "SliderArrange", "SliderArrangeFloat", "SliderArrangeBase", "SliderArrangeDictionary", "RangeRule", "SymbolType", "SliderArrangeType")},
    **{f"HikPcUI.Controls.{name}": f"SunEyeVision.Plugin.Wpf.Native.Numeric.HikPcUI.Controls.{name}" for name in ("NumericUpDown", "SliderArrangeType")},
    **{f"HikPcUI.Controls.{name}": f"SunEyeVision.Plugin.Wpf.Native.Numeric.HikPcUI.Controls.{name}" for name in ("BtnHeightConverter", "BtnShowConverter", "BtnShowGridConverter")},
    "VMControls.WPF.Release.Front.NumbericParamControl": "SunEyeVision.Plugin.Wpf.Native.Numeric.VMControls.WPF.Release.Front.NumbericParamControl",
}

def sha(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest().upper()

def text(path: Path) -> str:
    return path.read_text(encoding="utf-8-sig")

def adapt_host_source(path: Path, content: str) -> str:
    if path.name == "PolygonPoint.cs" and path.parent.name == "HikPcUI.ImageView":
        # This visual coordinate carrier uses no BaseNode members. Typed host contracts own persistence.
        assert "public class PolygonPoint : BaseNode, INotifyPropertyChanged" in content
        assert "using Hik.Xml;" in content
        return content.replace("using Hik.Xml;", "").replace(
            "public class PolygonPoint : BaseNode, INotifyPropertyChanged",
            "public class PolygonPoint : INotifyPropertyChanged")
    return content

def declared_types(content: str, namespace: str) -> list[str]:
    # Preserve nested type identity; a regex-only flat name list would alias e.g. PopupEx.RECT.
    stripped = re.sub(r'//[^\n]*|/\*[\s\S]*?\*/|@"(?:""|[^"])*"|"(?:\\.|[^"\\])*"|\x27(?:\\.|[^\x27\\])*\x27', lambda m: " " * len(m.group(0)), content)
    depth = 0
    stack = []
    pending = None
    result = []
    for match in re.finditer(r"\b(?:class|struct|enum|interface)\s+(\w+)|[{}]", stripped):
        if match.group(1):
            pending = match.group(1)
            result.append(namespace + "." + ".".join([name for _, name in stack] + [pending]))
        elif match.group(0) == "{":
            depth += 1
            if pending:
                stack.append((depth, pending))
                pending = None
        else:
            if stack and stack[-1][0] == depth: stack.pop()
            depth -= 1
    return result

def import_local_input(vision_ui: Path) -> tuple[list[dict], dict]:
    """Preserve the existing local reconstruction as its own source identity.

    Numeric owns SourceSliderDictionary independently; it is not interchangeable
    with the similarly named PcUI or Apps.XmlUI dictionaries.
    """
    names = ("SourceControlResources.cs", "SourceControlStyles.xaml", "SourceComboBoxes.xaml", "SourceScrollBars.xaml", "SourceBottomButtons.xaml")
    resource_root = "Themes/Native/Input/VisionUi/"
    asset_root = "Themes/Assets/Native/Input/VisionUi/SourceAssets/"
    numeric_uri = "/SunEyeVision.Plugin.Wpf;component/Themes/Native/Numeric/VisionUi/SourceSliderDictionary.xaml"
    images = set()
    records = []
    for name in names:
        source = vision_ui / name
        content = text(source)
        for path in re.findall(r"/VisionUi;component/SourceAssets/([^\"'<>]+)", content):
            images.add(path)
        if source.suffix == ".cs":
            content = "using System;\n" + content.replace("namespace VisionUi;", "namespace " + PREFIX + "VisionUi;")
            content = content.replace("/VisionUi;component/SourceControlStyles.xaml", "/SunEyeVision.Plugin.Wpf;component/" + resource_root + "SourceControlStyles.xaml")
            output = "src/Plugin.Wpf/Controls/Native/Input/VisionUi/" + name
            kind = "source"
        else:
            def uri(match):
                value = match.group(2)
                if value == "SourceSliderDictionary.xaml":
                    value = "pack://application:,,," + numeric_uri
                elif value in names and value.endswith(".xaml"):
                    value = "pack://application:,,,/SunEyeVision.Plugin.Wpf;component/" + resource_root + value
                elif value.startswith("/VisionUi;component/SourceAssets/"):
                    value = "pack://application:,,,/SunEyeVision.Plugin.Wpf;component/" + asset_root + value.split("/SourceAssets/", 1)[1]
                return match.group(1) + value + match.group(3)
            content = re.sub(r'(\b(?:Source|UriSource|Icon|Value)\s*=\s*["\'])([^"\']+)(["\'])', uri, content)
            output = "src/Plugin.Wpf/" + resource_root + name
            kind = "xaml"
        target = ROOT / output
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_text(content, encoding="utf-8", newline="\n")
        entry = dict(library="VisionUi.local-input", source=str(source), original_relative=name, kind=kind, source_sha256=sha(source.read_bytes()), output=output, output_sha256=sha(target.read_bytes()), status="transplanted_unbuilt", reasons=[], cross_group_dependencies=[])
        if name == "SourceControlResources.cs":
            entry.update(original_namespace="VisionUi", namespace=PREFIX + "VisionUi", types=["VisionUi.SourceControlResources"])
        if name == "SourceControlStyles.xaml":
            dependency = vision_ui / "SourceSliderDictionary.xaml"
            entry["cross_group_dependencies"].append(dict(owner="numeric", original=str(dependency), source_sha256=sha(dependency.read_bytes()), target_uri=numeric_uri))
        records.append(entry)
    for relative in sorted(images):
        source = vision_ui / "SourceAssets" / relative
        if not source.is_file(): raise FileNotFoundError("Local input template asset missing: " + str(source))
        output = "src/Plugin.Wpf/" + asset_root + relative
        target = ROOT / output
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(source.read_bytes())
        records.append(dict(library="VisionUi.local-input", source=str(source), original_relative="SourceAssets/" + relative, kind="asset", source_sha256=sha(source.read_bytes()), output=output, output_sha256=sha(target.read_bytes()), status="transplanted_unbuilt", reasons=[]))
    popup = Path(r"D:\MyWork\REA逆向测试\analysis\decompiled\VMControls.WPF\VMControls.WPF.Base\PopupEx.cs")
    sibling = Path(r"D:\MyWork\REA逆向测试\analysis\decompiled\VM.PcUI\HikPcUI.ListView\PopupWithTopmost.cs")
    return records, {
        "identity": "Existing local VisionUi reconstruction retained separately from original decompiled library identities; no original-binary equivalence claim",
        "source_root": str(vision_ui), "project_sha256": sha((vision_ui / "VisionUi.csproj").read_bytes()),
        "popup_review": {"original": str(popup), "original_sha256": sha(popup.read_bytes()), "same_type_restored": False, "finding": "PopupEx.UpdateWindow is empty in the available export. No same-type implementation found in local VisionUi. A different original PopupWithTopmost class has a real HwndSource/GetWindowRect/SetWindowPos implementation, retained under its own identity; it is not evidence of PopupEx's omitted method body.", "different_type_reference": str(sibling), "different_type_sha256": sha(sibling.read_bytes())},
        "limits": ["Only namespace/pack URI and explicit System import adapted", "No host source-catalog/workflow adapter added", "Root owns resource build items and dictionary merge", "No build, runtime, designer or visual tests"]
    }

def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--exports", type=Path, default=Path(r"D:\MyWork\REA逆向测试\analysis\decompiled"))
    parser.add_argument("--vision-ui", type=Path, default=Path(r"D:\MyWork\REA逆向测试\src\VisionUi"))
    args = parser.parse_args()
    inventory_path = ROOT / "docs/collaboration/evidence/VM_CONTROL_LIBRARY_INVENTORY_20261026.json"
    inventory = json.loads(inventory_path.read_text(encoding="utf-8-sig"))
    records = []
    namespaces = set()
    xaml_classes = {}
    sources_by_class = {}
    source_texts = {}
    library_meta = {item["name"]: item for item in inventory["libraries"] if item["name"] in LIBRARIES}
    original_build_items = {}
    for library in LIBRARIES:
        project = args.exports / library / (library + ".csproj")
        original_build_items[library] = {"project": str(project), "project_sha256": sha(project.read_bytes()), "resource_items": [dict(kind=node.tag.rsplit("}", 1)[-1], attributes=node.attrib, metadata={child.tag.rsplit("}", 1)[-1]: child.text for child in node}) for node in ET.fromstring(text(project)).iter() if node.tag.rsplit("}", 1)[-1] in {"Resource", "EmbeddedResource", "Content", "Page"}]}
    for library in LIBRARIES:
        folder = args.exports / library
        for source in sorted(folder.rglob("*")):
            if not source.is_file() or source.suffix.lower() not in {".cs", ".xaml", *IMAGE_EXT}:
                continue
            relative = source.relative_to(folder)
            if source.name == "AssemblyInfo.cs":
                continue  # Assembly identity is owned by the host project.
            kind = "asset" if source.suffix.lower() in IMAGE_EXT else "source" if source.suffix == ".cs" else "xaml"
            content = None if kind == "asset" else adapt_host_source(source, text(source))
            entry = {"library": library, "source": str(source), "original_relative": relative.as_posix(), "kind": kind,
                     "source_sha256": sha(source.read_bytes()), "reasons": [], "cross_group_dependencies": []}
            if source.name == "PolygonPoint.cs":
                entry["host_adaptations"] = ["Coordinate/index/INotifyPropertyChanged bodies retained; vendor Hik.Xml.BaseNode inheritance removed; typed host contracts own persistence, no vendor XML compatibility claimed"]
            if kind == "source":
                namespace = re.search(r"\bnamespace\s+([\w.]+)", content)
                if namespace:
                    entry["original_namespace"] = namespace.group(1)
                    entry["namespace"] = PREFIX + namespace.group(1)
                    namespaces.add(namespace.group(1))
                    entry["types"] = declared_types(content, namespace.group(1))
                    for name in (identity.rsplit(".", 1)[-1] for identity in entry["types"]):
                        sources_by_class.setdefault(name, []).append(entry)
                else:
                    entry["types"] = []
                source_texts[str(source)] = content
            elif kind == "xaml":
                element = ET.fromstring(content)
                entry["xclass"] = element.get("{" + XAML_NS + "}Class")
                entry["root_element"] = element.tag.rsplit("}", 1)[-1]
                if entry["xclass"]:
                    xaml_classes[(library, entry["xclass"])] = entry
            if (EXCLUDED.search(source.name) or source.stem in {"BtnHeightConverter", "BtnShowConverter", "BtnShowGridConverter"}) and kind != "asset":
                entry["status"] = "other_group_numeric"
            else:
                entry["status"] = "transplanted_unbuilt"
            if kind == "source" and ".Properties" in entry.get("original_namespace", ""):
                entry["reasons"].append("Original generated ResourceManager/Settings requires original embedded-resource/settings build contract; no substitute generated.")
            if kind == "source" and source.name == "PopupEx.cs":
                entry["reasons"].append("Original export PopupEx.UpdateWindow() has an empty body; native topmost/window positioning behavior is unavailable in this export.")
            records.append(entry)

    all_namespaces = sorted(namespaces, key=len, reverse=True)
    namespace_regex = re.compile(r"(?<![\w.])(?:[A-Za-z_]\w*\.)*[A-Za-z_]\w*")
    # Namespace roots are replaced once; retain all original suffixes and the distinct Release identity.
    def namespace_map(value: str) -> str:
        def replace(match):
            token = match.group(0)
            if value[match.end():].startswith(";component"):
                return token  # Assembly names in pack URIs are handled by the resource map.
            return PREFIX + token if any(token == ns or token.startswith(ns + ".") for ns in all_namespaces) else token
        return namespace_regex.sub(replace, value)

    # The output is intentionally split into active source and honest pending source.
    by_type = {type_name: record for record in records for type_name in record.get("types", [])}
    for record in records:
        if record["kind"] == "asset" or record["status"] == "other_group_numeric":
            continue
        content = adapt_host_source(Path(record["source"]), text(Path(record["source"])))
        if record["kind"] == "source":
            uses = re.findall(r"^using\s+(?:static\s+)?([\w.]+)\s*;", content, re.M)
            externals = [name for name in uses if not name.startswith("System") and not any(name == internal or name.startswith(internal + ".") for internal in namespaces)]
            for qualified in ("FrontendUI.Design", "VMControls.Winform", "WindowsFormsIntegration", "VM.PlatformSDK"):
                if qualified in content and qualified not in externals:
                    externals.append(qualified)
            if externals:
                record["reasons"].append("External namespace/interface closure not in this group: " + ", ".join(sorted(set(externals))))
            record["external_namespaces"] = sorted(set(externals))
            record["empty_void_method_sites"] = re.findall(r"\b(?:public|private|protected|internal)\s+(?:static\s+|override\s+|virtual\s+|async\s+)*void\s+(\w+)\s*\([^)]*\)\s*\{\s*\}", content)
        else:
            dependencies = re.findall(r"clr-namespace:([^;\"']+)(?:;assembly=([^\"']+))?", content)
            external = sorted(set((ns, assembly) for ns, assembly in dependencies if not ns.startswith(("System", "Microsoft.Windows.Themes")) and not any(ns == internal or ns.startswith(internal + ".") for internal in namespaces)))
            if external:
                record["reasons"].append("External XAML namespace closure: " + ", ".join(ns + (";assembly=" + assembly if assembly else "") for ns, assembly in external))
            record["external_xaml_namespaces"] = [dict(namespace=ns, assembly=assembly) for ns, assembly in external]
            record["xml_namespace_dependencies"] = sorted(set(re.findall(r'xmlns:\w+=["\']([^"\']+)["\']', content)))

    # Pair the XAML class with its real source and propagate known unavailable internal dependencies.
    for record in records:
        if record["kind"] != "source":
            continue
        pair = next((xaml_classes[(record["library"], identity)] for identity in record.get("types", []) if (record["library"], identity) in xaml_classes), None)
        if pair:
            record["paired_xaml"] = pair["source"]
            pair["paired_source"] = record["source"]
            if record["status"] == "other_group_numeric":
                pair["status"] = "other_group_numeric"
    for _ in range(len(records)):
        changed = False
        for record in records:
            if record["kind"] == "asset" or record["status"] == "other_group_numeric":
                continue
            reasons = list(record["reasons"])
            pair_source = record.get("paired_xaml") or record.get("paired_source")
            pair = next((item for item in records if item["source"] == pair_source), None) if pair_source else None
            if pair and pair["reasons"] and not any(reason.startswith("Paired original") for reason in reasons):
                reasons.append("Paired original XAML/code has unavailable dependencies; see " + pair["original_relative"])
            if record["kind"] == "source":
                content = source_texts[record["source"]]
                uses = re.findall(r"^using\s+([\w.]+)\s*;", content, re.M)
                for token in set(re.findall(r"\b[A-Z]\w*\b", content)):
                    for dependency in sources_by_class.get(token, []):
                        if dependency is record or not dependency["reasons"] or dependency["status"] == "other_group_numeric":
                            continue
                        ns = dependency.get("original_namespace")
                        if ns != record.get("original_namespace") and ns not in uses:
                            continue
                        marker = "Internal dependency pending: " + dependency["original_relative"]
                        if marker not in reasons:
                            reasons.append(marker)
            else:
                content = text(Path(record["source"]))
                aliases = dict(re.findall(r'xmlns:(\w+)=["\']clr-namespace:([^;"\']+)', content))
                for prefix, ns in aliases.items():
                    for name in set(re.findall(r"\b" + re.escape(prefix) + r":(\w+)", content)):
                        dependency = by_type.get(ns + "." + name)
                        if dependency and dependency is not record and dependency["reasons"] and dependency["status"] != "other_group_numeric":
                            marker = "Internal XAML type pending: " + dependency["original_relative"]
                            if marker not in reasons: reasons.append(marker)
                for owner, path in re.findall(r'(?:pack://application:,,,)?/([^/;]+);component/([^"\']+)', content):
                    dependency = next((item for item in records if item["library"].lower() == owner.lower() and item["original_relative"].lower() == path.lower()), None)
                    if dependency and dependency is not record and dependency["reasons"] and dependency["status"] != "other_group_numeric":
                        marker = "Referenced original resource pending: " + dependency["original_relative"]
                        if marker not in reasons: reasons.append(marker)
            if reasons != record["reasons"]:
                record["reasons"] = reasons
                changed = True
        if not changed:
            break

    for record in records:
        relative = Path(record["original_relative"])
        if record["status"] == "other_group_numeric":
            continue
        if record["kind"] == "asset":
            destination = Path("src/Plugin.Wpf/Themes/Assets/Native/Input") / record["library"] / relative
        elif record["kind"] == "source" or record.get("xclass"):
            destination = Path("src/Plugin.Wpf/Controls/Native/Input") / record["library"] / relative
        else:
            destination = Path("src/Plugin.Wpf/Themes/Native/Input") / record["library"] / relative
        if record["reasons"]:
            destination = Path(str(destination) + ".pending")
            record["status"] = "transplanted_pending_real_dependency"
        record["output"] = destination.as_posix()

    # Remove only this importer's exact generated targets, including a previous pending/active variant.
    # No directory is recursively removed; original exports and other migration groups are untouched.
    for record in records:
        relative = Path(record["original_relative"])
        if record["kind"] == "asset":
            parent = Path("src/Plugin.Wpf/Themes/Assets/Native/Input")
        elif record["kind"] == "source" or record.get("xclass"):
            parent = Path("src/Plugin.Wpf/Controls/Native/Input")
        else:
            parent = Path("src/Plugin.Wpf/Themes/Native/Input")
        expected = ROOT / parent / record["library"] / relative
        for candidate in (expected, Path(str(expected) + ".pending")):
            if not candidate.resolve().is_relative_to((ROOT / parent).resolve()):
                raise ValueError("Output escapes this importer's directory: " + str(candidate))
            if candidate.exists(): candidate.unlink()
    resource_map = {(record["library"].lower(), record["original_relative"].lower()): record["output"] for record in records if "output" in record}
    # Exact sibling paths confirmed by the numeric migration writer, not fabricated local templates.
    resource_map[("vm.pcui", "listview/sliderarrangedictionary.xaml")] = "src/Plugin.Wpf/Themes/Native/Numeric/PcUi/SliderArrangeDictionary.xaml"
    missing_resources = []
    def adapt_resource(value: str, library: str, original_relative: str) -> str:
        value = value.replace("\\", "/")
        match = re.match(r"(?:pack://application:,,,)?/?([^/;]+);component/(.+)$", value, re.I)
        if match:
            owner, relative = match.groups()
            if owner not in LIBRARIES:
                return value
        else:
            if value.startswith(("{", "http:", "https:", "file:")) or not re.search(r"\.(?:xaml|png|jpg|jpeg|bmp|gif|ico|cur)$", value, re.I):
                return value
            owner = library
            if value.startswith("/"):
                relative = value.lstrip("/")
            else:
                parts = list(Path(original_relative).parent.parts)
                for part in value.split("/"):
                    if part == "..":
                        if parts: parts.pop()
                    elif part not in ("", "."):
                        parts.append(part)
                relative = "/".join(parts)
        output = resource_map.get((owner.lower(), relative.lower()))
        if output is None and "{" in relative:
            source_parent = relative.rsplit("/", 1)[0].lower()
            siblings = [mapped for (lib, path), mapped in resource_map.items() if lib == owner.lower() and path.rsplit("/", 1)[0] == source_parent]
            if siblings:
                output = siblings[0].rsplit("/", 1)[0] + "/" + relative.rsplit("/", 1)[1]
        if output is None:
            missing_resources.append(dict(library=library, file=original_relative, reference=value, owner=owner, relative=relative))
            return value
        # A pending file remains pending. Root must restore the real dependency before enabling it.
        return "pack://application:,,,/SunEyeVision.Plugin.Wpf;component/" + output.removeprefix("src/Plugin.Wpf/").removesuffix(".pending")

    for record in records:
        if "output" not in record:
            continue
        source = Path(record["source"])
        destination = ROOT / record["output"]
        destination.parent.mkdir(parents=True, exist_ok=True)
        if record["kind"] == "asset":
            data = source.read_bytes()
        else:
            content = adapt_host_source(source, text(source))
            if record["kind"] == "xaml":
                # Rewrite namespaces and assembly ownership, preserving the actual original template body.
                content = re.sub(r'clr-namespace:([^;"\']+)(?:;assembly=([^"\']+))?', lambda match: "clr-namespace:" + namespace_map(match.group(1)) + (";assembly=SunEyeVision.Plugin.Wpf" if match.group(2) in LIBRARIES else ";assembly=" + match.group(2) if match.group(2) else ""), content)
                if record.get("xclass"):
                    content = content.replace('x:Class="' + record["xclass"] + '"', 'x:Class="' + PREFIX + record["xclass"] + '"')
                aliases = dict(re.findall(r'xmlns:([\w]+)="clr-namespace:([^;"\']+)', content))
                new_namespaces = {}
                for prefix, namespace in aliases.items():
                    original_namespace = namespace.removeprefix(PREFIX)
                    for original_type, numeric_type in NUMERIC_TYPES.items():
                        ns, name = original_type.rsplit(".", 1)
                        if original_namespace != ns or prefix + ":" + name not in content:
                            continue
                        numeric_prefix = "nativeNumeric" + str(len(new_namespaces))
                        content = re.sub(r"\b" + re.escape(prefix + ":" + name) + r"\b", numeric_prefix + ":" + name, content)
                        new_namespaces[numeric_prefix] = numeric_type.rsplit(".", 1)[0]
                        record["cross_group_dependencies"].append(numeric_type)
                if new_namespaces:
                    position = re.search(r"<(?!\?)[\w.:]+", content).end()
                    content = content[:position] + "".join(f' xmlns:{key}="clr-namespace:{value};assembly=SunEyeVision.Plugin.Wpf"' for key, value in new_namespaces.items()) + content[position:]
                content = re.sub(r'(\b(?:Source|UriSource|Icon|Value)\s*=\s*["\'])([^"\']+)(["\'])', lambda match: match.group(1) + adapt_resource(match.group(2), record["library"], record["original_relative"]) + match.group(3), content)
            else:
                content = namespace_map(content)
                aliases = []
                for original, target in NUMERIC_TYPES.items():
                    ns, name = original.rsplit(".", 1)
                    if re.search(r"\b" + re.escape(name) + r"\b", content) and ("using " + PREFIX + ns + ";" in content or PREFIX + original in content):
                        content = content.replace(PREFIX + original, target)
                        aliases.append(f"using {name} = {target};")
                        record["cross_group_dependencies"].append(target)
                if aliases:
                    content = "\n".join(sorted(set(aliases))) + "\n" + content
                def csharp_resource(match):
                    original = match.group(1)
                    adapted = adapt_resource(original, record["library"], record["original_relative"])
                    # Preserve the original UriKind contract: an assembly-relative URI stays relative.
                    if not original.startswith("pack://application:,,,"):
                        adapted = adapted.removeprefix("pack://application:,,,")
                    return '"' + adapted + '"'
                content = re.sub(r'"((?:pack://application:,,,)?/?[^";\n]+;component/[^"\n]+)"', csharp_resource, content)
                # XAML compiler regenerates named fields. Remove only exact single fields declared by this x:Class's own XAML.
                if record.get("paired_xaml"):
                    element = ET.fromstring(text(Path(record["paired_xaml"])))
                    names = set()
                    def visit_names(node):
                        if node.tag.rsplit("}", 1)[-1] in {"ControlTemplate", "DataTemplate", "ItemsPanelTemplate", "Style"}:
                            return
                        names.add(node.get("{" + XAML_NS + "}Name") or node.get("Name"))
                        for child in node: visit_names(child)
                    visit_names(element)
                    removed = []
                    for name in names - {None}:
                        pattern = r"(?m)^\s*(?:private|internal|public|protected)\s+(?:[\w.<>]+)\s+" + re.escape(name) + r"\s*;\s*$"
                        content, count = re.subn(pattern, "", content)
                        if count: removed.append(name)
                    if removed: record["xaml_regenerated_fields"] = sorted(removed)
            data = content.encode("utf-8")
        destination.write_bytes(data)
        record["output_sha256"] = sha(data)

    local_records, local_evidence = import_local_input(args.vision_ui)
    records.extend(local_records)
    namespaces.add("VisionUi")
    manifest = {
        "baseline": "b87fb6857212ce8e1a7d0d0cb47c6c9a00c8e86b",
        "group": "native-input", "status": "source_transplant_only_no_build_no_test_no_availability_claim",
        "namespace_policy": PREFIX + "<original namespace>",
        "source_inventory": str(inventory_path.relative_to(ROOT)), "source_inventory_sha256": sha(inventory_path.read_bytes()),
        "libraries": {name: dict(root=str(args.exports / name), assembly=name, nonframework_references=[ref["assembly"] for ref in library_meta[name]["references"] if not ref.get("framework")]) for name in LIBRARIES},
        "adaptations": ["Original namespaces prefixed without merging library identities", "Internal pack/resource URI changed to host assembly and migrated path", "Only XAML-generated named field duplicates removed from matching partial source", "Numeric sibling types redirected to agreed Native.Numeric namespaces", "Actual unavailable source/templates preserved with .pending suffix; no invented stubs"],
        "summary": {"files": len(records), "active_source_or_xaml": sum(r["status"] == "transplanted_unbuilt" and r["kind"] != "asset" for r in records), "pending_source_or_xaml": sum(r["status"] == "transplanted_pending_real_dependency" for r in records), "assets": sum(r["kind"] == "asset" for r in records), "numeric_other_group": sum(r["status"] == "other_group_numeric" for r in records)},
        "files": records, "resource_gaps": missing_resources,
        "namespace_map": {namespace: PREFIX + namespace for namespace in sorted(namespaces)},
        "original_build_items": original_build_items,
        "local_input_supplement": local_evidence,
        "type_map": [{"assembly": record["library"], "original_full_type": original, "target_full_type": NUMERIC_TYPES.get(original, PREFIX + original), "status": record["status"]} for record in records for original in record.get("types", [])],
        "type_mappings": [{"assembly": record["library"], "original": original, "migrated": NUMERIC_TYPES.get(original, PREFIX + original), "status": record["status"], "source": record["source"], "output": record.get("output")} for record in records for original in record.get("types", [])],
        "integration_required": ["Root owns project Resource/Page/Compile items, embedded resources and Generic merge order", "Resolve external library ownership/namespace maps from other migration groups before enabling pending files", "WindowsForms wrappers require the original unavailable FrontendUI/VMControls.Winform behavior and host interface adaptation", "PopupEx.UpdateWindow original export is empty; do not claim topmost behavior", "Runtime/designer/native/visual verification deliberately deferred to root integration"]
    }
    destination = ROOT / "src/Plugin.Wpf/Controls/Native/Input/migration-manifest.json"
    destination.write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8")
    provenance = ROOT / "src/Plugin.Wpf/Themes/Native/Input/provenance.json"
    provenance.parent.mkdir(parents=True, exist_ok=True)
    provenance.write_text(json.dumps({"group": "native-input", "manifest": "Controls/Native/Input/migration-manifest.json", "files": [{key: value for key, value in record.items() if key in ("library", "source", "original_relative", "kind", "source_sha256", "output", "output_sha256", "status", "reasons")} for record in records if record["kind"] in ("xaml", "asset")]}, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps(manifest["summary"], ensure_ascii=False))

if __name__ == "__main__":
    main()
