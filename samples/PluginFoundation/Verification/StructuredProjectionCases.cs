using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf;

internal static partial class Program
{
    private static async Task StructuredProjectionCases()
    {
        await Case("FT-F01-projection", "Structured editor follows declaration order and nested sequence indices",()=>ProjectionSta(()=>{
            var child=TypeRef.RecordOf("child",new[]{new Port("z",DataType.Integer),new Port("a",DataType.Text,false,true)});
            var rows=TypeRef.SequenceOf(child);
            var root=TypeRef.RecordOf("root",new[]{new Port("rows",rows),new Port("nullable",DataType.Text,false,true),
                new Port("missing",DataType.Text,false,true),new Port("empty",TypeRef.SequenceOf(TypeRef.ScalarOf(ScalarKind.Boolean))),
                new Port("text",DataType.Text),new Port("numbers",DataType.NumberArray)});
            var fields=new Dictionary<string,object?>{
                {"text",""},{"numbers",new[]{1.25,2.5}},{"empty",Array.Empty<bool>()},{"nullable",null},
                {"rows",new[]{new Dictionary<string,object?>{{"a","tail"},{"z",2L}},new Dictionary<string,object?>{{"z",1L}}}}};
            var session=ProjectionSession(root,new RecordValue(root,fields));
            var editor=new ParameterEditorControl(session,"value");
            Require(editor.Input.Text=="{ \"rows\" = [ [0] = { \"z\" = 2, \"a\" = \"tail\" }, [1] = { \"z\" = 1, \"a\" = <missing> } ], \"nullable\" = <null>, \"missing\" = <missing>, \"empty\" = [ <empty> ], \"text\" = \"\" <empty>, \"numbers\" = [ [0] = 1.25, [1] = 2.5 ] }","declaration/index/missing/null/empty projection");
            var emptyRecord=TypeRef.RecordOf("empty",Array.Empty<Port>());
            Require(new ParameterEditorControl(ProjectionSession(emptyRecord,new RecordValue(emptyRecord,new Dictionary<string,object?>())),"value").Input.Text=="{ <empty> }","empty record");
        }));
        await Case("FT-F03-projection-readonly", "Repeated refresh and programmatic edit/click cannot change structured committed or drafts",()=>ProjectionSta(()=>{
            var type=TypeRef.SequenceOf(TypeRef.ScalarOf(ScalarKind.Int64));
            var original=new SequenceValue(type,new object?[]{3L,4L});
            var session=new ParameterEditSession(new[]{new Port("value",type),new Port("other",DataType.Number)},
                new Dictionary<string,ParameterSetting>{{"value",new ConstantSetting(original)},{"other",new ConstantSetting(1d)}});
            var editor=new ParameterEditorControl(session,"value");
            Require(editor.Input.IsReadOnly&&!editor.Commit.IsEnabled,"structure is editable");
            Require(session.SetText("other","9"),"pending setup");
            for(var i=0;i<3;i++){editor.Input.Text="programmatic draft";editor.RefreshFromCommitted();}
            editor.Commit.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Require(!editor.LastCommitSucceeded&&!session.HasInvalidDraft,"readonly event reached SetText or committed");
            Require(((ConstantSetting)session.Committed["other"]).Value is 1d,"readonly click committed another pending value");
            Require(ReferenceEquals(((ConstantSetting)session.Committed["value"]).Value,original),"refresh replaced committed carrier");
            Require(editor.Input.Text=="[ [0] = 3, [1] = 4 ]","repeat projection changed");
            Require(session.SetText("value","sentinel-invalid")==false,"sentinel setup");
            editor.RefreshFromCommitted();
            Require(session.DraftError("value")=="sentinel-invalid","refresh called SetText");
        }));
        await Case("FT-F01-projection-culture", "Structured scalars use invariant culture and distinguish literal marker text",()=>ProjectionSta(()=>{
            var prior=CultureInfo.CurrentCulture;var priorUi=CultureInfo.CurrentUICulture;
            try
            {
                CultureInfo.CurrentCulture=new CultureInfo("fr-FR");CultureInfo.CurrentUICulture=new CultureInfo("fr-FR");
                var type=TypeRef.SequenceOf(TypeRef.ScalarOf(ScalarKind.Float64));
                var editor=new ParameterEditorControl(ProjectionSession(type,new SequenceValue(type,new object?[]{1.25,2.5})),"value");
                Require(editor.Input.Text=="[ [0] = 1.25, [1] = 2.5 ]","current culture leaked");
                var textType=TypeRef.SequenceOf(TypeRef.ScalarOf(ScalarKind.Text));
                var text=new ParameterEditorControl(ProjectionSession(textType,new SequenceValue(textType,new object?[]{"<null>","a\nb"})),"value").Input.Text;
                Require(text.Contains("\\u003Cnull\\u003E")&&text.Contains("a\\nb"),"literal markers or newlines ambiguous");
            }
            finally{CultureInfo.CurrentCulture=prior;CultureInfo.CurrentUICulture=priorUi;}
        }));
        await Case("FT-F03-scalar-reference", "Scalar edit validation and reference read-only projection remain available",()=>ProjectionSta(()=>{
            var session=ProjectionSession(DataType.Number,1d);var editor=new ParameterEditorControl(session,"value");
            Require(!editor.Input.IsReadOnly&&editor.Commit.IsEnabled,"scalar blocked");
            editor.Input.Text="bad";editor.Commit.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Require(!editor.LastCommitSucceeded&&((ConstantSetting)session.Committed["value"]).Value is 1d,"invalid scalar committed");
            editor.Input.Text="2.5";editor.Commit.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Require(editor.LastCommitSucceeded&&((ConstantSetting)session.Committed["value"]).Value is 2.5d,"scalar commit changed");
            var reference=new ReferenceSetting(new OutputReference("source","port",DataType.Number));
            Require(session.Set("value",reference)&&session.TryCommit(),"reference setup");editor.RefreshFromCommitted();
            Require(editor.Input.IsReadOnly&&editor.Input.Text=="source.port","reference display changed");
            editor.Input.Text="blocked";editor.Commit.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Require(!session.HasInvalidDraft&&session.Committed["value"]==reference&&!editor.LastCommitSucceeded,"reference write escaped");
            Require(session.RestoreConstant("value")&&session.TryCommit(),"restore setup");editor.RefreshFromCommitted();
            Require(!editor.Input.IsReadOnly&&editor.Commit.IsEnabled,"scalar gate did not reopen");
        }));
    }
    private static ParameterEditSession ProjectionSession(TypeRef type,object value)=>new(new[]{new Port("value",type)},
        new Dictionary<string,ParameterSetting>{{"value",new ConstantSetting(value)}});
    private static Task ProjectionSta(Action action)
    {
        var completion=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread=new Thread(()=>{try{action();completion.SetResult();}catch(Exception e){completion.SetException(e);}}){IsBackground=true};
        thread.SetApartmentState(ApartmentState.STA);thread.Start();return completion.Task;
    }
}
