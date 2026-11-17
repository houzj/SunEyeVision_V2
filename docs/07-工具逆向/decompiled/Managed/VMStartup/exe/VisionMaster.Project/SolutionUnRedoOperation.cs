using System;
using System.IO;
using VMControls.Interface;

namespace VisionMaster.Project;

public class SolutionUnRedoOperation : DisplayNotify, IUnRedoActionOperation, IUnRedoOperation, IDisposable, IUnRedoDataDone<UnRedo_SolutionData>, IDisposeTempFile
{
	public bool IsPart { get; set; } = true;

	public UnRedo_SolutionData Data { get; set; }

	public IUnRedoParams Params { get; set; }

	public IBuilder ParentNode { get; set; }

	public Action<UnRedo_SolutionData, IUnRedoParams> RedoAction { get; set; }

	public Action<UnRedo_SolutionData, IUnRedoParams> UndoAction { get; set; }

	public Action<UnRedo_SolutionData> UpdateAction { get; set; }

	public bool IsDataComplete { get; set; } = true;

	public ExecutePriority ExecutePriority { get; set; }

	public string TempPath { get; set; }

	public SolutionUnRedoOperation(Action<UnRedo_SolutionData, IUnRedoParams> redoAction, Action<UnRedo_SolutionData, IUnRedoParams> undoAction, UnRedo_SolutionData data)
	{
		RedoAction = redoAction;
		UndoAction = undoAction;
		Data = data;
	}

	public void Redo()
	{
		RedoAction(Data, Params);
	}

	public void Undo()
	{
		UndoAction(Data, Params);
	}

	public void Update()
	{
	}

	public void Dispose()
	{
		RedoAction = null;
		UndoAction = null;
		UpdateAction = null;
		if (Data.Path != null && Data.Path.Contains("TempFiles") && File.Exists(Data.Path) && TempPath != Data.Path)
		{
			File.Delete(Data.Path);
		}
		Data = null;
		TempPath = null;
	}

	public void DeleteFile(object data)
	{
		TempPath = ((data as IUnRedoDataDone<UnRedo_SolutionData>)?.Data)?.Path;
		data = null;
	}
}
