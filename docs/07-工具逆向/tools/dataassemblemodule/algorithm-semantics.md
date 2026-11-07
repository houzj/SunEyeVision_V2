# 协议组装 VM algorithm semantics

- stableToolId: dataassemblemodule
- status: inventory-only; source-confirmed schema only

## Confirmed schema evidence
- Inputs: 
- Outputs: ModuRunTime, ModuStatus, AssembleResult, AssembleResultHex
- Parameters: 
- UI controls: AssembleType, Separator, PyScriptPath, FiledHead, InputParams
- XML/PE/resource hashes: resources.json

## Execution and lifecycle (unknown)

| Stage | Conclusion |
|---|---|
| Process/ProcessAlg | Tool-specific body, ABI and return codes not recovered. |
| Result publication | Output names are XML declarations; writer order, null/empty behavior and verdict unknown. |
| Display | Display XML/resources indexed; runtime producer/template behavior unknown. |
| Save/restore | Serialization keys, migration and rebinding unknown. |
| Reader/release | Reader leases, temporary ownership, DeInit/Destroy order unknown. |

No algorithm behavior is inferred from field names or intended product semantics.
