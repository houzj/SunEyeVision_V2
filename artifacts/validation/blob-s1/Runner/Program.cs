using System.Text.Json;
var stages=new List<string>();
try {
if(Environment.GetEnvironmentVariable("BLOB_S1_NEGATIVE_CONTROL")=="1")throw new InvalidOperationException("INTENTIONAL_NEGATIVE_CONTROL");
ResourceContractCases.Run();stages.Add("ResourceContractCases.Run");
S1ContractMigrationCases.Run();stages.Add("S1ContractMigrationCases.Run");
Console.WriteLine(JsonSerializer.Serialize(new{status="PASS",stages,scope="S1 neutral contract and source-linked policies only; not full Workflow/native/UI/Release"}));
return 0;
} catch(Exception e) {Console.Error.WriteLine(e);return 1;}

