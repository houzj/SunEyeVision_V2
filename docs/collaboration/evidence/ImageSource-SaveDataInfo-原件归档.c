
/* public: void __cdecl CMyAlgorithmManager::SaveDataInfo(struct SAVE_MODULE_DATA_PARAM * __ptr64
   const,char * __ptr64,int,char const * __ptr64) __ptr64 */

void __thiscall
CMyAlgorithmManager::SaveDataInfo
          (CMyAlgorithmManager *this,SAVE_MODULE_DATA_PARAM *param_1,char *param_2,int param_3,
          char *param_4)

{
  longlong lVar1;
  
                    /* 0x184e0  34
                       ?SaveDataInfo@CMyAlgorithmManager@@QEAAXQEAUSAVE_MODULE_DATA_PARAM@@PEADHPEBD@Z
                        */
  if ((((param_1 != (SAVE_MODULE_DATA_PARAM *)0x0) && (param_2 != (char *)0x0)) &&
      (param_4 != (char *)0x0)) && (*(int *)(param_1 + 0x58490) < 0x80)) {
    lVar1 = (longlong)*(int *)(param_1 + 0x58490) * 0x118;
    *(int *)(param_1 + lVar1 + 0x585a8) = param_3;
    *(char **)(param_1 + lVar1 + 0x585a0) = param_2;
    _snprintf_s((char *)(param_1 + lVar1 + 0x58498),0x104,0x103,"%s",param_4);
    *(int *)(param_1 + 0x58490) = *(int *)(param_1 + 0x58490) + 1;
  }
  return;
}

