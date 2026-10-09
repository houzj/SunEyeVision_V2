using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using SunEyeVision.Plugin.Infrastructure.Managers.Plugin;
using SunEyeVision.Workflow;
using SunEyeVision.Workflow.Persistence;
using Xunit;

namespace SunEyeVision.UI.Tests;

public sealed class SolutionManagementTests
{
    private sealed class Fixture : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "SunEyeVision-SolutionManagement", Guid.NewGuid().ToString("N"));
        public ApplicationDataLocationService Locations { get; }
        public ApplicationDataPaths Paths { get; }
        public Fixture()
        {
            Directory.CreateDirectory(Root);
            Locations = new ApplicationDataLocationService(Path.Combine(Root, "locator.json"));
            Paths = Locations.Prepare(Path.Combine(Root, "data"));
            PluginLoader.MarkAsLoaded();
        }
        public SolutionManager Manager() => new(Paths);
        public string File(string name) => Path.Combine(Root, name);
        public void Dispose()
        {
            foreach (var path in Directory.EnumerateFiles(Root, "*", SearchOption.AllDirectories)) System.IO.File.SetAttributes(path, FileAttributes.Normal);
            Directory.Delete(Root, true);
        }
    }
    private static SolutionMetadata Create(SolutionManager manager, string name = "方案A") =>
        manager.CreateNewSolution(new SolutionMetadata { Name = name, Description = "可传递的描述", DirectoryPath = manager.SolutionsDirectory });

    [Fact]
    public void Configuration_and_default_solutions_are_separate_and_locator_can_be_rebuilt()
    {
        using var fixture = new Fixture();
        var manager = fixture.Manager();
        var metadata = Create(manager);
        fixture.Locations.Remember(fixture.Paths);
        Assert.Equal(fixture.Paths.ConfigFile, manager.ConfigurationPath);
        Assert.StartsWith(fixture.Paths.SolutionsDirectory, metadata.FilePath);
        Assert.False(System.IO.File.Exists(Path.Combine(manager.SolutionsDirectory, "solution_settings.json")));
        var configuration = System.IO.File.ReadAllText(fixture.Paths.ConfigFile);
        System.IO.File.Delete(fixture.Locations.LocatorFile);
        Assert.Null(fixture.Locations.Locate());
        fixture.Locations.Remember(fixture.Locations.Prepare(fixture.Paths.Root));
        Assert.Equal(fixture.Paths.Root, fixture.Locations.Locate()!.Root);
        Assert.Equal(configuration, System.IO.File.ReadAllText(fixture.Paths.ConfigFile));
    }

    [Fact]
    public void Missing_or_corrupt_configuration_is_not_replaced_with_empty_settings()
    {
        using var fixture = new Fixture();
        _ = fixture.Manager();
        fixture.Locations.Remember(fixture.Paths);
        System.IO.File.WriteAllText(fixture.Paths.ConfigFile, "{broken");
        Assert.ThrowsAny<Exception>(() => fixture.Locations.Locate());
        Assert.ThrowsAny<Exception>(() => fixture.Manager());
        Assert.Equal("{broken", System.IO.File.ReadAllText(fixture.Paths.ConfigFile));
        System.IO.File.Delete(fixture.Paths.ConfigFile);
        Assert.ThrowsAny<Exception>(() => fixture.Locations.Locate());
    }

    [Fact]
    public void Failed_configuration_save_rolls_back_new_file_and_keeps_dirty_preferences()
    {
        using var fixture = new Fixture();
        var manager = fixture.Manager();
        var original = System.IO.File.ReadAllText(fixture.Paths.ConfigFile);
        System.IO.File.SetAttributes(fixture.Paths.ConfigFile, FileAttributes.ReadOnly);
        Assert.ThrowsAny<IOException>(() => Create(manager, "应回滚"));
        Assert.Empty(manager.GetAllMetadata());
        Assert.False(System.IO.File.Exists(Path.Combine(manager.SolutionsDirectory, "应回滚.solution")));
        manager.Settings.SetPreference("keep", 7); manager.MarkSettingsDirty();
        Assert.ThrowsAny<IOException>(() => manager.SaveUserSettings());
        Assert.Equal(original, System.IO.File.ReadAllText(fixture.Paths.ConfigFile));
        System.IO.File.SetAttributes(fixture.Paths.ConfigFile, FileAttributes.Normal);
        manager.SaveUserSettings();
        Assert.Equal(7, fixture.Manager().Settings.GetPreference<int>("keep"));
    }

    [Fact]
    public void Broken_target_open_never_closes_or_saves_current_edits()
    {
        using var fixture = new Fixture();
        var manager = fixture.Manager(); var metadata = Create(manager);
        manager.SetCurrentSolution(manager.LoadSolutionOnly(metadata.FilePath)!);
        manager.CurrentSolution!.Description = "未保存内容";
        var original = System.IO.File.ReadAllText(metadata.FilePath);
        var broken = fixture.File("broken.solution"); System.IO.File.WriteAllText(broken, "{broken");
        Assert.Null(manager.OpenSolution(broken));
        Assert.Equal(metadata.Id, manager.CurrentSolution.Id);
        Assert.True(manager.HasUnsavedChanges);
        Assert.Equal(original, System.IO.File.ReadAllText(metadata.FilePath));
    }

    [Fact]
    public void Nested_preferences_keep_json_types_including_window_layout()
    {
        using var fixture = new Fixture();
        var manager = fixture.Manager();
        manager.Settings.SetPreference("WindowLayout", new SunEyeVision.UI.Shared.LayoutConfig { LeftColumnWidth = 340, IsRightPanelCollapsed = true });
        manager.MarkSettingsDirty(); manager.SaveUserSettings();
        var restored = fixture.Manager();
        var layout = restored.Settings.GetPreference<SunEyeVision.UI.Shared.LayoutConfig>("WindowLayout");
        Assert.NotNull(layout); Assert.Equal(340, layout!.LeftColumnWidth); Assert.True(layout.IsRightPanelCollapsed);
        restored.MarkSettingsDirty(); restored.SaveUserSettings();
        using var json = JsonDocument.Parse(System.IO.File.ReadAllText(fixture.Paths.ConfigFile));
        Assert.Equal(JsonValueKind.Object, json.RootElement.GetProperty("UserPreferences").GetProperty("WindowLayout").ValueKind);
    }

    [Fact]
    public void Locator_rejects_replaced_data_identity_at_the_same_path()
    {
        using var fixture = new Fixture(); _ = fixture.Manager(); fixture.Locations.Remember(fixture.Paths);
        var marker = JsonNode.Parse(System.IO.File.ReadAllText(fixture.Paths.MarkerFile))!.AsObject();
        marker["dataId"] = Guid.NewGuid().ToString();
        AtomicJsonFile.Write(fixture.Paths.MarkerFile, marker.ToJsonString());
        Assert.Throws<InvalidDataException>(() => fixture.Locations.Locate());
    }

    [Fact]
    public void Failed_migration_locator_write_rolls_back_target_and_leaves_source_intact()
    {
        using var fixture = new Fixture(); var manager = fixture.Manager(); var metadata = Create(manager);
        var original = System.IO.File.ReadAllText(metadata.FilePath);
        var target = fixture.Locations.Prepare(fixture.File("target"));
        var blocker = fixture.File("locator-blocker"); System.IO.File.WriteAllText(blocker, "file");
        var broken = new ApplicationDataLocationService(Path.Combine(blocker, "locator.json"));
        Assert.ThrowsAny<IOException>(() => broken.MigrateData(fixture.Paths, target));
        Assert.Equal(original, System.IO.File.ReadAllText(metadata.FilePath));
        Assert.False(System.IO.File.Exists(target.ConfigFile));
        Assert.Empty(Directory.EnumerateFiles(target.SolutionsDirectory, "*", SearchOption.AllDirectories));
        target.ValidateAvailable();
    }

    [Fact]
    public void Previous_save_restores_content_without_scattered_backup_or_resource_deletion()
    {
        using var fixture = new Fixture(); var manager = fixture.Manager(); var metadata = Create(manager);
        manager.SetCurrentSolution(manager.LoadSolutionOnly(metadata.FilePath)!);
        var previous = System.IO.File.ReadAllText(metadata.FilePath);
        manager.CurrentSolution!.Description = "第二次保存"; Assert.True(manager.SaveSolution());
        Assert.True(manager.HasPreviousSave(metadata.Id));
        var restored = manager.RestorePreviousSave(metadata.Id);
        Assert.Equal(metadata.Id, restored.Id);
        Assert.Equal(metadata.FilePath, restored.FilePath);
        Assert.Equal(previous, System.IO.File.ReadAllText(metadata.FilePath));
        Assert.False(System.IO.File.Exists(metadata.FilePath + ".bak"));
    }

    [Fact]
    public void Unopened_cross_directory_copy_keeps_relative_resource_identity()
    {
        using var fixture = new Fixture(); var manager = fixture.Manager(); var metadata = Create(manager);
        var document = JsonNode.Parse(System.IO.File.ReadAllText(metadata.FilePath))!.AsObject();
        document["VendorData"] = JsonNode.Parse("{\"FilePaths\":[\"images/a.png\"]}");
        AtomicJsonFile.Write(metadata.FilePath, document.ToJsonString());
        var target = fixture.File("other-directory"); Directory.CreateDirectory(target);
        var copied = manager.CopySolution(metadata.FilePath, "跨目录副本", targetDirectory: target)!;
        using var result = JsonDocument.Parse(System.IO.File.ReadAllText(copied.FilePath));
        Assert.Equal(Path.Combine(manager.SolutionsDirectory, "images", "a.png"), result.RootElement.GetProperty("VendorData").GetProperty("FilePaths")[0].GetString());
    }

    [Fact]
    public void Rename_keeps_embedded_name_identity_and_previous_version_available()
    {
        using var fixture = new Fixture(); var manager = fixture.Manager(); var metadata = Create(manager);
        manager.SetCurrentSolution(manager.LoadSolutionOnly(metadata.FilePath)!);
        manager.CurrentSolution!.Description = "有上一版本"; Assert.True(manager.SaveSolution());
        Assert.True(manager.RenameSolutionFile(metadata.Id, "重命名方案"));
        var changed = manager.GetMetadata(metadata.Id)!;
        Assert.Equal("重命名方案", manager.LoadSolutionOnly(changed.FilePath)!.Name);
        Assert.Equal(metadata.Id, manager.CurrentSolution.Id);
        Assert.True(manager.HasPreviousSave(metadata.Id));
        Assert.False(System.IO.File.Exists(metadata.FilePath));
    }

    [Fact]
    public void Rename_configuration_failure_restores_original_file_and_current_path()
    {
        using var fixture = new Fixture(); var manager = fixture.Manager(); var metadata = Create(manager);
        manager.SetCurrentSolution(manager.LoadSolutionOnly(metadata.FilePath)!);
        var original = System.IO.File.ReadAllText(metadata.FilePath);
        System.IO.File.SetAttributes(fixture.Paths.ConfigFile, FileAttributes.ReadOnly);
        Assert.False(manager.RenameSolutionFile(metadata.Id, "不能生效"));
        Assert.Equal(metadata.FilePath, manager.CurrentFilePath);
        Assert.Equal(original, System.IO.File.ReadAllText(metadata.FilePath));
        Assert.False(System.IO.File.Exists(Path.Combine(manager.SolutionsDirectory, "不能生效.solution")));
    }

    [Fact]
    public void Metadata_configuration_failure_restores_original_solution_content()
    {
        using var fixture = new Fixture(); var manager = fixture.Manager(); var metadata = Create(manager);
        var original = System.IO.File.ReadAllText(metadata.FilePath);
        var edited = metadata.Clone(); edited.Description = "不应提交的描述";
        System.IO.File.SetAttributes(fixture.Paths.ConfigFile, FileAttributes.ReadOnly);
        Assert.ThrowsAny<IOException>(() => manager.UpdateMetadata(edited));
        Assert.Equal(original, System.IO.File.ReadAllText(metadata.FilePath));
        Assert.Equal(metadata.Description, manager.GetMetadata(metadata.Id)!.Description);
    }

    [Fact]
    public void Structurally_incomplete_configuration_is_not_silently_overwritten()
    {
        using var fixture = new Fixture(); _ = fixture.Manager();
        System.IO.File.WriteAllText(fixture.Paths.ConfigFile, "{}");
        Assert.ThrowsAny<InvalidDataException>(() => fixture.Paths.ValidateAvailable());
        Assert.ThrowsAny<InvalidDataException>(() => fixture.Manager());
        Assert.Equal("{}", System.IO.File.ReadAllText(fixture.Paths.ConfigFile));
    }

    [Fact]
    public void Legacy_identity_mismatch_rolls_back_prior_supplements_and_does_not_commit_configuration()
    {
        using var fixture = new Fixture();
        var old = fixture.File("legacy"); Directory.CreateDirectory(old);
        var first = Path.Combine(old, "first.solution"); var second = Path.Combine(old, "second.solution");
        var firstId = Guid.NewGuid().ToString();
        var firstJson = JsonSerializer.Serialize(new { Id = firstId, Version = "1.0", Workflows = Array.Empty<object>() });
        System.IO.File.WriteAllText(first, firstJson);
        System.IO.File.WriteAllText(second, JsonSerializer.Serialize(new { Id = Guid.NewGuid().ToString(), Version = "1.0", Workflows = Array.Empty<object>() }));
        var settings = new SolutionSettings(); settings.SetBasePath(old);
        settings.AddKnownSolution(new SolutionMetadata { Id = firstId, Name = "应回滚补充", FilePath = first, DirectoryPath = old });
        settings.AddKnownSolution(new SolutionMetadata { Id = Guid.NewGuid().ToString(), Name = "身份不匹配", FilePath = second, DirectoryPath = old });
        var config = Path.Combine(old, "solution_settings.json"); Assert.True(settings.Save(config));
        Assert.Throws<InvalidDataException>(() => fixture.Locations.ImportLegacy(config, fixture.Paths));
        Assert.Equal(firstJson, System.IO.File.ReadAllText(first));
        Assert.False(System.IO.File.Exists(fixture.Paths.ConfigFile));
    }

    [Theory]
    [InlineData("Choose")][InlineData("Last")][InlineData("Default")]
    public void Startup_mode_and_preferences_survive_reloading(string mode)
    {
        using var fixture = new Fixture();
        var manager = fixture.Manager();
        manager.Settings.StartupMode = mode;
        manager.Settings.SetPreference("Theme", 1);
        manager.MarkSettingsDirty(); manager.SaveUserSettings();
        var reloaded = fixture.Manager();
        Assert.Equal(mode, reloaded.Settings.StartupMode);
        Assert.Equal(1, reloaded.Settings.GetPreference<int>("Theme"));
    }

    [Fact]
    public void New_solution_never_closes_current_and_never_overwrites_same_name()
    {
        using var fixture = new Fixture();
        var manager = fixture.Manager();
        var first = Create(manager);
        manager.SetCurrentSolution(manager.LoadSolutionOnly(first.FilePath)!);
        manager.CurrentSolution!.Description = "尚未保存";
        var second = Create(manager, "方案B");
        Assert.Equal(first.Id, manager.CurrentSolution.Id);
        var original = System.IO.File.ReadAllText(second.FilePath);
        Assert.ThrowsAny<IOException>(() => Create(manager, "方案B"));
        Assert.Equal(original, System.IO.File.ReadAllText(second.FilePath));
        Assert.Equal("尚未保存", manager.CurrentSolution.Description);
    }

    [Fact]
    public void Save_as_updates_current_identity_and_subsequent_save_keeps_new_identity()
    {
        using var fixture = new Fixture();
        var manager = fixture.Manager();
        var original = Create(manager);
        manager.SetCurrentSolution(manager.LoadSolutionOnly(original.FilePath)!);
        var originalJson = System.IO.File.ReadAllText(original.FilePath);
        manager.CurrentSolution!.Description = "最新编辑内容";
        var target = fixture.File("另存方案.solution");
        var copy = manager.SaveAsSolution(target, "另存方案", "最新编辑内容")!;
        Assert.NotEqual(original.Id, copy.Id);
        Assert.Equal(copy.Id, manager.CurrentSolution!.Id);
        Assert.Equal(target, manager.CurrentFilePath);
        Assert.Equal(copy.Id, manager.Settings.CurrentSolutionId);
        Assert.True(manager.SaveSolution());
        using var json = JsonDocument.Parse(System.IO.File.ReadAllText(target));
        Assert.Equal(copy.Id, json.RootElement.GetProperty("Id").GetString());
        Assert.Equal("最新编辑内容", json.RootElement.GetProperty("Description").GetString());
        Assert.Equal(originalJson, System.IO.File.ReadAllText(original.FilePath));
        Assert.False(System.IO.File.Exists(target + ".bak"));
    }

    [Fact]
    public void Copy_preserves_latest_content_format_and_source_without_switching()
    {
        using var fixture = new Fixture();
        var manager = fixture.Manager();
        var original = Create(manager);
        manager.SetCurrentSolution(manager.LoadSolutionOnly(original.FilePath)!);
        manager.CurrentSolution!.Description = "未保存但需复制的内容";
        var copy = manager.CopySolution(original.FilePath, "副本", newDescription: manager.CurrentSolution.Description, targetDirectory: fixture.Root)!;
        var copied = manager.LoadSolutionOnly(copy.FilePath)!;
        Assert.NotEqual(original.Id, copied.Id);
        Assert.Equal("未保存但需复制的内容", copied.Description);
        Assert.Equal(manager.CurrentSolution.SchemaVersion, copied.SchemaVersion);
        Assert.Equal(original.Id, manager.CurrentSolution.Id);
        Assert.ThrowsAny<IOException>(() => manager.CopySolution(original.FilePath, "副本", targetDirectory: fixture.Root));
        Assert.ThrowsAny<IOException>(() => manager.SaveAsSolution(original.FilePath, original.Name));
    }

    [Fact]
    public void Unopened_copy_keeps_unknown_fields_and_metadata_travels_with_file()
    {
        using var fixture = new Fixture();
        var manager = fixture.Manager();
        var original = Create(manager);
        var json = JsonNode.Parse(System.IO.File.ReadAllText(original.FilePath))!.AsObject();
        json["ExtraPortableField"] = "保留扩展字段";
        System.IO.File.WriteAllText(original.FilePath, json.ToJsonString());
        var copy = manager.CopySolution(original.FilePath, "另一电脑", newDescription: "随文件传递", targetDirectory: fixture.Root)!;
        using var document = JsonDocument.Parse(System.IO.File.ReadAllText(copy.FilePath));
        Assert.Equal("保留扩展字段", document.RootElement.GetProperty("ExtraPortableField").GetString());
        var receivingRoot = fixture.Locations.Prepare(fixture.File("receiver"));
        var receiver = new SolutionManager(receivingRoot);
        var loaded = receiver.LoadSolutionOnly(copy.FilePath)!;
        receiver.RegisterKnownSolution(new SolutionRepository().LoadMetadata(copy.FilePath)!);
        receiver.SetCurrentSolution(loaded);
        Assert.Equal("另一电脑", loaded.Name);
        Assert.Equal("随文件传递", loaded.Description);
        Assert.Empty(receiver.Settings.DefaultSolutionId);
    }

    [Fact]
    public void Duplicate_identity_different_paths_requires_explicit_replacement()
    {
        using var fixture = new Fixture();
        var manager = fixture.Manager();
        var original = Create(manager);
        var target = fixture.File("移动后的方案.solution");
        System.IO.File.Copy(original.FilePath, target);
        var moved = new SolutionRepository().LoadMetadata(target)!;
        Assert.ThrowsAny<InvalidDataException>(() => manager.RegisterKnownSolution(moved));
        Assert.Equal(original.FilePath, manager.GetMetadata(original.Id)!.FilePath);
        manager.ReplaceKnownSolution(moved);
        Assert.Equal(target, manager.GetMetadata(original.Id)!.FilePath);
        Assert.Single(manager.GetAllMetadata());
    }

    [Fact]
    public void Missing_records_remain_visible_and_removal_keeps_other_files()
    {
        using var fixture = new Fixture();
        var manager = fixture.Manager();
        var missing = Create(manager);
        var valid = Create(manager, "其他方案");
        System.IO.File.Delete(missing.FilePath);
        manager.RefreshMetadata();
        Assert.Contains(manager.GetAllMetadata(), metadata => metadata.Id == missing.Id && metadata.IsMissing);
        Assert.True(manager.DeleteSolution(missing.Id, false));
        Assert.True(System.IO.File.Exists(valid.FilePath));
    }

    [Fact]
    public void Only_opening_changes_recent_access_time()
    {
        using var fixture = new Fixture();
        var manager = fixture.Manager();
        var metadata = Create(manager);
        Assert.Equal(DateTime.MinValue, manager.GetMetadata(metadata.Id)!.LastAccessTime);
        metadata.Description = "仅编辑描述";
        manager.UpdateMetadata(metadata);
        Assert.Equal(DateTime.MinValue, manager.GetMetadata(metadata.Id)!.LastAccessTime);
        manager.SetCurrentSolution(manager.LoadSolutionOnly(metadata.FilePath)!);
        Assert.True(manager.GetMetadata(metadata.Id)!.LastAccessTime > DateTime.MinValue);
        Assert.False(manager.HasUnsavedChanges);
        manager.CurrentSolution!.Description = "修改";
        Assert.True(manager.HasUnsavedChanges);
        Assert.True(manager.SaveSolution());
        Assert.False(manager.HasUnsavedChanges);
    }

    [Fact]
    public void Failed_save_retains_unsaved_state_and_original_file()
    {
        using var fixture = new Fixture();
        var manager = fixture.Manager();
        var metadata = Create(manager);
        manager.SetCurrentSolution(manager.LoadSolutionOnly(metadata.FilePath)!);
        var original = System.IO.File.ReadAllText(metadata.FilePath);
        manager.CurrentSolution!.Description = "不能丢失的编辑";
        System.IO.File.SetAttributes(metadata.FilePath, FileAttributes.ReadOnly);
        Assert.False(manager.SaveSolution());
        Assert.True(manager.HasUnsavedChanges);
        Assert.Equal(original, System.IO.File.ReadAllText(metadata.FilePath));
        Assert.ThrowsAny<Exception>(() => manager.CloseSolution());
        Assert.NotNull(manager.CurrentSolution);
    }

    [Fact]
    public void Legacy_migration_preserves_relative_paths_preferences_and_source_configuration()
    {
        using var fixture = new Fixture();
        var legacyDirectory = fixture.File("desktop"); Directory.CreateDirectory(legacyDirectory);
        var source = Path.Combine(legacyDirectory, "旧方案.solution");
        var id = Guid.NewGuid().ToString();
        System.IO.File.WriteAllText(source, JsonSerializer.Serialize(new { Id = id, Version = "1.0", Workflows = Array.Empty<object>(), Devices = Array.Empty<object>() }));
        var settings = new SolutionSettings(); settings.SetBasePath(legacyDirectory);
        settings.SetPreference("Theme", 1); settings.StartupMode = "Last"; settings.CurrentSolutionId = id;
        var accessed = new DateTime(2026, 10, 1);
        settings.AddKnownSolution(new SolutionMetadata { Id = id, Name = "随文件传递的名称", Description = "保留的描述", FilePath = source, DirectoryPath = legacyDirectory, LastAccessTime = accessed });
        var config = Path.Combine(legacyDirectory, "solution_settings.json"); Assert.True(settings.Save(config));
        var originalConfiguration = System.IO.File.ReadAllText(config);
        fixture.Locations.ImportLegacy(config, fixture.Paths);
        var manager = fixture.Manager();
        Assert.Equal(source, manager.GetMetadata(id)!.FilePath);
        Assert.Equal(accessed, manager.GetMetadata(id)!.LastAccessTime);
        Assert.Equal("Last", manager.Settings.StartupMode);
        Assert.Equal(1, manager.Settings.GetPreference<int>("Theme"));
        var loaded = manager.LoadSolutionOnly(source)!;
        Assert.Equal("随文件传递的名称", loaded.Name); Assert.Equal("保留的描述", loaded.Description);
        Assert.Equal(originalConfiguration, System.IO.File.ReadAllText(config));
        var first = System.IO.File.ReadAllText(fixture.Paths.ConfigFile);
        fixture.Locations.ImportLegacy(config, fixture.Paths);
        Assert.Equal(first, System.IO.File.ReadAllText(fixture.Paths.ConfigFile));
        Assert.False(Directory.Exists(Path.Combine(fixture.Paths.Root, "Backups", "Config")));
    }

    [Fact]
    public void Moving_data_keeps_external_paths_and_relocates_internal_paths()
    {
        using var fixture = new Fixture();
        var manager = fixture.Manager();
        var internalFile = Create(manager);
        var outside = fixture.File("外部方案.solution");
        System.IO.File.WriteAllText(outside, JsonSerializer.Serialize(new { Id = "external", Name = "外部方案", Workflows = Array.Empty<object>() }));
        manager.RegisterKnownSolution(new SolutionRepository().LoadMetadata(outside)!); manager.SaveUserSettings();
        fixture.Locations.Remember(fixture.Paths);
        var target = fixture.Locations.Prepare(fixture.File("new-data"));
        fixture.Locations.MigrateData(fixture.Paths, target);
        var migrated = new SolutionManager(target);
        Assert.Equal(Path.Combine(target.SolutionsDirectory, Path.GetFileName(internalFile.FilePath)), migrated.GetMetadata(internalFile.Id)!.FilePath);
        Assert.Equal(outside, migrated.GetMetadata("external")!.FilePath);
        Assert.True(System.IO.File.Exists(internalFile.FilePath));
        Assert.Equal(target.Root, fixture.Locations.Locate()!.Root);
    }

    [Fact]
    public void Future_schema_is_rejected_without_modifying_file()
    {
        using var fixture = new Fixture();
        var path = fixture.File("future.solution");
        var json = "{\"Id\":\"future\",\"SchemaVersion\":999,\"Workflows\":[]}";
        System.IO.File.WriteAllText(path, json);
        var repository = new SolutionRepository(fixture.Paths.RecoveryDirectory);
        Assert.Null(repository.Load(path));
        Assert.Null(repository.LoadMetadata(path));
        Assert.Equal(json, System.IO.File.ReadAllText(path));
    }

    [Theory]
    [InlineData("")][InlineData("..")][InlineData("CON")][InlineData("COM1")][InlineData("a/b")][InlineData("a.")]
    public void Invalid_names_are_rejected(string name) => Assert.ThrowsAny<ArgumentException>(() => AtomicJsonFile.ValidateSolutionName(name));
}
