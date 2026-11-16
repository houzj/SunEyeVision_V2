
/* WARNING: Function: __security_check_cookie replaced with injection: security_check_cookie */
/* public: void __cdecl CMyAlgorithmManager::SaveImageFileToSol(class std::basic_string<char,struct
   std::char_traits<char>,class std::allocator<char> >,struct SAVE_MODULE_DATA_PARAM * __ptr64
   const) __ptr64 */

void __thiscall
CMyAlgorithmManager::SaveImageFileToSol
          (CMyAlgorithmManager *this,char *param_2,SAVE_MODULE_DATA_PARAM *param_3)

{
  void *_DstBuf;
  char cVar1;
  int iVar2;
  uint uVar3;
  wchar_t *_Filename;
  FILE *_File;
  undefined8 *puVar4;
  CMyAlgorithmManager *pCVar5;
  undefined8 uVar6;
  size_t sVar7;
  char *pcVar8;
  int iVar9;
  int iVar10;
  uint uVar11;
  LPCSTR pCVar12;
  int iVar13;
  int iVar14;
  longlong lVar15;
  undefined1 auStackY_b8 [32];
  undefined2 local_68;
  undefined6 uStack_66;
  undefined8 local_58;
  ulonglong local_50;
  ulonglong local_48;
  
                    /* 0x18210  35
                       ?SaveImageFileToSol@CMyAlgorithmManager@@QEAAXV?$basic_string@DU?$char_traits@D@std@@V?$allocator@D@2@@std@@QEAUSAVE_MODULE_DATA_PARAM@@@Z
                        */
  local_48 = DAT_18003c000 ^ (ulonglong)auStackY_b8;
  if (param_3 == (SAVE_MODULE_DATA_PARAM *)0x0) {
    if (0xf < *(ulonglong *)(param_2 + 0x18)) {
      operator_delete(*(void **)param_2);
    }
    param_2[0x18] = '\x0f';
    param_2[0x19] = '\0';
    param_2[0x1a] = '\0';
    param_2[0x1b] = '\0';
    param_2[0x1c] = '\0';
    param_2[0x1d] = '\0';
    param_2[0x1e] = '\0';
    param_2[0x1f] = '\0';
    param_2[0x10] = '\0';
    param_2[0x11] = '\0';
    param_2[0x12] = '\0';
    param_2[0x13] = '\0';
    param_2[0x14] = '\0';
    param_2[0x15] = '\0';
    param_2[0x16] = '\0';
    param_2[0x17] = '\0';
    *param_2 = '\0';
    return;
  }
  pCVar12 = param_2;
  if (0xf < *(ulonglong *)(param_2 + 0x18)) {
    pCVar12 = *(LPCSTR *)param_2;
  }
  _Filename = (wchar_t *)FUN_18000aca0(&local_68,pCVar12);
  if (7 < *(ulonglong *)(_Filename + 0xc)) {
    _Filename = *(wchar_t **)_Filename;
  }
  _File = _wfopen(_Filename,L"rb+");
  if (7 < local_50) {
    operator_delete((void *)CONCAT62(uStack_66,local_68));
  }
  local_50 = 7;
  iVar9 = 0;
  local_58 = 0;
  local_68 = 0;
  if (_File == (FILE *)0x0) goto LAB_180018496;
  iVar2 = fseek(_File,0,2);
  if (iVar2 < 0) {
    cVar1 = HSlog_ShouldLog(4);
    if (cVar1 != '\0') {
      puVar4 = FUN_180001320();
      if (0xf < (ulonglong)puVar4[3]) {
        puVar4 = (undefined8 *)*puVar4;
      }
LAB_180018464:
      HSlog_LogPrint(4,"[%s] [ModuId:%d] [%s@%d] error nRet = %#X\r\n",puVar4,
                     *(undefined4 *)(this + 0x24938));
    }
  }
  else {
    uVar3 = ftell(_File);
    fseek(_File,0,0);
    iVar2 = *(int *)(param_3 + 0x58490);
    if ((*(void **)(this + (longlong)iVar2 * 0x10 + 0x24948) != (void *)0x0) &&
       (uVar3 != *(uint *)(this + (longlong)iVar2 * 0x10 + 0x24954))) {
      operator_delete__(*(void **)(this + (longlong)iVar2 * 0x10 + 0x24948));
      *(undefined4 *)(this + (longlong)iVar2 * 0x10 + 0x24954) = 0;
      *(undefined8 *)(this + (longlong)iVar2 * 0x10 + 0x24948) = 0;
    }
    if (*(longlong *)(this + (longlong)iVar2 * 0x10 + 0x24948) == 0) {
      pCVar5 = this + 0x24964;
      lVar15 = 0x41;
      iVar10 = iVar9;
      iVar13 = iVar9;
      iVar14 = iVar9;
      do {
        iVar9 = iVar9 + *(int *)(pCVar5 + -0x10);
        iVar14 = iVar14 + *(int *)pCVar5;
        iVar13 = iVar13 + *(int *)(pCVar5 + 0x10);
        iVar10 = iVar10 + *(int *)(pCVar5 + 0x20);
        pCVar5 = pCVar5 + 0x40;
        lVar15 = lVar15 + -1;
      } while (lVar15 != 0);
      if ((int)(iVar14 + iVar13 + iVar10 + iVar9 + uVar3) < 0xc800000) {
        uVar6 = thunk_FUN_180028f10((longlong)(int)uVar3);
        *(undefined8 *)(this + (longlong)iVar2 * 0x10 + 0x24948) = uVar6;
      }
    }
    _DstBuf = *(void **)(this + (longlong)iVar2 * 0x10 + 0x24948);
    if (_DstBuf != (void *)0x0) {
      uVar11 = uVar3;
      if (uVar3 < *(uint *)(this + (longlong)iVar2 * 0x10 + 0x24954)) {
        uVar11 = *(uint *)(this + (longlong)iVar2 * 0x10 + 0x24954);
      }
      *(uint *)(this + (longlong)iVar2 * 0x10 + 0x24954) = uVar11;
      sVar7 = fread(_DstBuf,(longlong)(int)uVar3,1,_File);
      if (sVar7 == 1) {
        pcVar8 = param_2;
        if (0xf < *(ulonglong *)(param_2 + 0x18)) {
          pcVar8 = *(char **)param_2;
        }
        SaveDataInfo(this,param_3,*(char **)(this + (longlong)iVar2 * 0x10 + 0x24948),uVar3,pcVar8);
      }
      else {
        cVar1 = HSlog_ShouldLog(4);
        if (cVar1 != '\0') {
          puVar4 = FUN_180001320();
          if (0xf < (ulonglong)puVar4[3]) {
            puVar4 = (undefined8 *)*puVar4;
          }
          goto LAB_180018464;
        }
      }
    }
  }
  fclose(_File);
LAB_180018496:
  if (0xf < *(ulonglong *)(param_2 + 0x18)) {
    operator_delete(*(void **)param_2);
  }
  param_2[0x18] = '\x0f';
  param_2[0x19] = '\0';
  param_2[0x1a] = '\0';
  param_2[0x1b] = '\0';
  param_2[0x1c] = '\0';
  param_2[0x1d] = '\0';
  param_2[0x1e] = '\0';
  param_2[0x1f] = '\0';
  param_2[0x10] = '\0';
  param_2[0x11] = '\0';
  param_2[0x12] = '\0';
  param_2[0x13] = '\0';
  param_2[0x14] = '\0';
  param_2[0x15] = '\0';
  param_2[0x16] = '\0';
  param_2[0x17] = '\0';
  *param_2 = '\0';
  return;
}

