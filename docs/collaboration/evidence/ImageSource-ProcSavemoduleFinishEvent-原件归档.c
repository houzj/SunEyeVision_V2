
/* public: void __cdecl CMyAlgorithmManager::ProcSavemoduleFinishEvent(void) __ptr64 */

void __thiscall CMyAlgorithmManager::ProcSavemoduleFinishEvent(CMyAlgorithmManager *this)

{
  CMyAlgorithmManager *pCVar1;
  longlong lVar2;
  
                    /* 0x1a3b0  32  ?ProcSavemoduleFinishEvent@CMyAlgorithmManager@@QEAAXXZ */
  pCVar1 = this + 0x24948;
  lVar2 = 0x104;
  do {
    if (*(void **)pCVar1 != (void *)0x0) {
      operator_delete__(*(void **)pCVar1);
      *(undefined8 *)pCVar1 = 0;
      *(undefined8 *)(pCVar1 + 8) = 0;
    }
    pCVar1 = pCVar1 + 0x10;
    lVar2 = lVar2 + -1;
  } while (lVar2 != 0);
  *(undefined8 *)(this + 0x25a78) = 0;
  if (*(ulonglong *)(this + 0x25a80) < 0x10) {
    this[0x25a68] = (CMyAlgorithmManager)0x0;
  }
  else {
    **(undefined1 **)(this + 0x25a68) = 0;
  }
  return;
}

