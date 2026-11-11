
/* WARNING: Function: __security_check_cookie replaced with injection: security_check_cookie */
/* WARNING: Removing unreachable block (ram,0x000180018bc6) */
/* WARNING: Removing unreachable block (ram,0x000180018c7d) */
/* public: virtual int __cdecl CMyAlgorithmManager::LoadModuleData(struct LOAD_MODULE_DATA_PARAM
   const * __ptr64) __ptr64 */

int __thiscall
CMyAlgorithmManager::LoadModuleData(CMyAlgorithmManager *this,LOAD_MODULE_DATA_PARAM *param_1)

{
  CMyAlgorithmManager CVar1;
  CMyAlgorithmManager CVar2;
  code *pcVar3;
  undefined1 auVar4 [16];
  undefined8 *****pppppuVar5;
  undefined8 ****ppppuVar6;
  char cVar7;
  int iVar8;
  errno_t eVar9;
  int iVar10;
  LPCWSTR pWVar11;
  longlong lVar12;
  undefined8 *puVar13;
  ushort *puVar14;
  undefined8 ******ppppppuVar15;
  undefined8 *puVar16;
  wchar_t ******ppppppwVar17;
  FILE *pFVar18;
  undefined8 *puVar19;
  CMyAlgorithmManager *pCVar20;
  undefined8 uVar21;
  wchar_t *_Filename;
  char *pcVar22;
  __uint64 _Var23;
  void *_Dst;
  int *piVar24;
  ulonglong uVar25;
  LOAD_MODULE_DATA_PARAM *pLVar26;
  longlong *_Buf1;
  longlong lVar27;
  longlong *plVar28;
  longlong *plVar29;
  uint *puVar30;
  ushort *puVar31;
  LOAD_MODULE_DATA_PARAM *pLVar32;
  LOAD_MODULE_DATA_PARAM *pLVar33;
  uint uVar34;
  LOAD_MODULE_DATA_PARAM *pLVar35;
  ulonglong uVar36;
  ulonglong uVar37;
  CMyAlgorithmManager *pCVar38;
  LOAD_MODULE_DATA_PARAM *pLVar39;
  void *pvVar40;
  ulonglong uVar41;
  CMyAlgorithmManager *pCVar42;
  longlong lVar43;
  bool bVar44;
  undefined1 auStackY_d38 [32];
  uint local_ce8 [2];
  CMyAlgorithmManager *local_ce0;
  int local_cd8;
  LOAD_MODULE_DATA_PARAM *local_cd0;
  CMyAlgorithmManager *local_cc8;
  LOAD_MODULE_DATA_PARAM *local_cc0;
  int local_cb8 [2];
  LOAD_MODULE_DATA_PARAM *local_cb0;
  LOAD_MODULE_DATA_PARAM *local_ca8;
  LOAD_MODULE_DATA_PARAM *pLStack_ca0;
  undefined8 local_c98;
  longlong *local_c90 [2];
  undefined8 *local_c80;
  longlong lStack_c78;
  undefined8 local_c70;
  undefined8 local_c68;
  wchar_t *****local_c60 [2];
  ulonglong local_c50;
  ulonglong local_c48;
  undefined8 local_c40;
  undefined8 local_c30;
  ulonglong local_c28;
  undefined8 *****local_c20 [2];
  undefined8 ****local_c10;
  undefined8 ****local_c08;
  undefined8 local_c00;
  ulonglong local_bf0;
  ulonglong local_be8;
  longlong local_be0 [2];
  ulonglong local_bd0;
  ulonglong local_bc8;
  longlong local_bc0 [5];
  undefined8 local_b98;
  undefined1 uStack_b90;
  undefined8 local_b8f;
  undefined7 uStack_b87;
  undefined1 local_b80;
  undefined7 uStack_b7f;
  undefined1 uStack_b78;
  undefined8 local_b77;
  undefined8 local_b6f;
  undefined8 local_b67;
  undefined4 local_b5f;
  undefined2 local_b5b;
  undefined1 local_b59;
  char local_b58;
  undefined1 local_b57 [783];
  undefined8 local_848;
  ulonglong local_48;
  
                    /* 0x18560  26
                       ?LoadModuleData@CMyAlgorithmManager@@UEAAHPEBULOAD_MODULE_DATA_PARAM@@@Z */
  local_c68 = 0xfffffffffffffffe;
  local_48 = DAT_18003c000 ^ (ulonglong)auStackY_d38;
  local_ce0 = this;
  local_cd0 = param_1;
  if (*(longlong *)(param_1 + 0x610d8) == 0) {
    cVar7 = HSlog_ShouldLog();
    if (cVar7 != '\0') {
      if ((DAT_18003cf00 & 1) == 0) {
        DAT_18003cf00 = DAT_18003cf00 | 1;
        FUN_180001360();
        atexit(FUN_18002c980);
      }
      puVar16 = &DAT_18003cee0;
      if (0xf < DAT_18003cef8) {
        puVar16 = DAT_18003cee0;
      }
      HSlog_LogPrint(4,
                     "[%s] [ModuId:%d] [%s@%d] pstLoadInfo->struModuleData.pszAttachmentsDir == NULL\r\n"
                     ,puVar16,*(undefined4 *)(this + 0x24938));
    }
  }
  else {
    cVar7 = HSlog_ShouldLog(4);
    if (cVar7 != '\0') {
      if ((DAT_18003cf00 & 1) == 0) {
        DAT_18003cf00 = DAT_18003cf00 | 1;
        FUN_180001360();
        atexit(FUN_18002c980);
      }
      puVar16 = &DAT_18003cee0;
      if (0xf < DAT_18003cef8) {
        puVar16 = DAT_18003cee0;
      }
      HSlog_LogPrint(4,
                     "[%s] [ModuId:%d] [%s@%d] pstLoadInfo->struModuleData.pszAttachmentsDir == %s\r\n"
                     ,puVar16,*(undefined4 *)(this + 0x24938));
    }
  }
  SetAllParamList(this,(PARAM_VALUE_INFO_LIST *)param_1);
  pCVar20 = this + 0x3b8;
  plVar28 = (longlong *)**(longlong **)pCVar20;
  local_cc8 = pCVar20;
  if (plVar28 != *(longlong **)pCVar20) {
    do {
      plVar29 = plVar28 + 2;
      if (0xf < (ulonglong)plVar28[5]) {
        plVar29 = (longlong *)*plVar29;
      }
      pWVar11 = (LPCWSTR)FUN_18000aca0(&local_b98,(LPCSTR)plVar29);
      if (7 < *(ulonglong *)(pWVar11 + 0xc)) {
        pWVar11 = *(LPCWSTR *)pWVar11;
      }
      DeleteFileW(pWVar11);
      if (7 < CONCAT71(uStack_b7f,local_b80)) {
        operator_delete((void *)CONCAT71(local_b98._1_7_,(char)local_b98));
      }
      plVar28 = (longlong *)*plVar28;
    } while (plVar28 != (longlong *)*(longlong *)pCVar20);
  }
  GetRootPath(this);
  (**(code **)(*(longlong *)this + 0x18))(this,"ClearImage",&DAT_1800300f8,1);
  local_ca8 = (LOAD_MODULE_DATA_PARAM *)0x0;
  pLStack_ca0 = (LOAD_MODULE_DATA_PARAM *)0x0;
  uVar25 = 0;
  local_c98 = 0;
  local_c80 = (undefined8 *)0x0;
  lStack_c78 = 0;
  local_c70 = 0;
  local_cb8[0] = 0;
  pLVar39 = param_1 + 0x58490;
  local_cb0 = pLVar39;
  if (0 < *(int *)pLVar39) {
    pLVar35 = param_1 + 0x585a0;
    uVar37 = uVar25;
LAB_180018790:
    pLVar32 = param_1 + (longlong)(int)uVar37 * 0x118 + 0x58498;
    iVar8 = strcmp((char *)pLVar32,"DynamicInData");
    if (iVar8 == 0) {
      (**(code **)(*(longlong *)local_ce0 + 0x18))
                (local_ce0,"DynamicInData",*(undefined8 *)pLVar35,*(undefined4 *)(pLVar35 + 8));
    }
    else {
      iVar8 = strcmp((char *)pLVar32,"DynamicOutData");
      uVar41 = uVar25;
      if (iVar8 != 0) {
LAB_180018810:
        uVar36 = uVar41;
        if (pLVar32[uVar36] == *(LOAD_MODULE_DATA_PARAM *)("Version" + uVar36))
        goto code_r0x00018001881d;
        iVar8 = strcmp((char *)pLVar32,"ImageNameList");
        if (iVar8 == 0) {
          pcVar22 = *(char **)(pLVar35 + (longlong)(param_1 + (0x58490 - (longlong)pLVar39)));
          local_c28 = 0xf;
          local_c30 = 0;
          local_c40._0_1_ = 0;
          uVar41 = uVar25;
          if (*pcVar22 != '\0') {
            uVar41 = 0xffffffffffffffff;
            do {
              uVar41 = uVar41 + 1;
            } while (pcVar22[uVar41] != '\0');
          }
          FUN_180001aa0(&local_c40,pcVar22,uVar41);
          local_be8 = 0xf;
          local_bf0 = 0;
          local_c00._0_1_ = 0;
          FUN_180001ce0(&local_c00,&local_c40,0,0xffffffffffffffff);
          StringSplitVector(local_ce0,&local_c00,&DAT_18002f7fc,&local_ca8);
        }
        else {
          iVar8 = strcmp((char *)pLVar32,"SelectedImageList");
          if (iVar8 != 0) goto LAB_180018964;
          pcVar22 = *(char **)(pLVar35 + (longlong)(param_1 + (0x58490 - (longlong)pLVar39)));
          local_c28 = 0xf;
          local_c30 = 0;
          local_c40._0_1_ = 0;
          uVar41 = uVar25;
          if (*pcVar22 != '\0') {
            uVar41 = 0xffffffffffffffff;
            do {
              uVar41 = uVar41 + 1;
            } while (pcVar22[uVar41] != '\0');
          }
          FUN_180001aa0(&local_c40,pcVar22,uVar41);
          local_be8 = 0xf;
          local_bf0 = 0;
          local_c00._0_1_ = 0;
          FUN_180001ce0(&local_c00,&local_c40,0,0xffffffffffffffff);
          StringSplitVector(local_ce0,&local_c00,&DAT_18002f7fc,&local_c80);
        }
        if (0xf < local_c28) {
          operator_delete((void *)CONCAT62(local_c40._2_6_,
                                           CONCAT11(local_c40._1_1_,(undefined1)local_c40)));
        }
        goto LAB_180018964;
      }
      (**(code **)(*(longlong *)local_ce0 + 0x18))
                (local_ce0,"DynamicOutData",*(undefined8 *)pLVar35,*(undefined4 *)(pLVar35 + 8));
    }
    goto LAB_180018964;
  }
LAB_1800197a9:
  local_cd8 = 0;
  if (0 < *(int *)pLVar39) {
    pLVar26 = param_1 + 0x585a8;
    pLVar32 = local_ca8;
    pLVar35 = local_ca8;
    do {
      iVar8 = local_cd8;
      pLVar33 = local_cd0 + (longlong)local_cd8 * 0x118 + 0x58498;
      iVar10 = strcmp((char *)pLVar33,"ImageNameList");
      if (((iVar10 != 0) && (iVar10 = strcmp((char *)pLVar33,"DynamicInData"), iVar10 != 0)) &&
         (iVar10 = strcmp((char *)pLVar33,"DynamicOutData"), iVar10 != 0)) {
        if ((pLVar35 != pLStack_ca0) && (pLVar33 = pLVar32, 0xf < *(ulonglong *)(pLVar32 + 0x18))) {
          pLVar33 = *(LOAD_MODULE_DATA_PARAM **)pLVar32;
        }
        local_b58 = '\0';
        memset(local_b57,0,0x30b);
        uVar25 = 0xffffffffffffffff;
        do {
          uVar25 = uVar25 + 1;
        } while (pLVar33[uVar25] != (LOAD_MODULE_DATA_PARAM)0x0);
        uVar21 = FUN_18000c860((byte *)pLVar33,uVar25);
        if ((int)uVar21 == 0) {
          FUN_18000aba0(&local_c40,(LPCSTR)pLVar33);
          uVar21 = 0x30b;
          _snprintf_s(&local_b58,0x30c,0x30b,"%s");
          if (0xf < local_c28) {
            operator_delete((void *)CONCAT62(local_c40._2_6_,
                                             CONCAT11(local_c40._1_1_,(undefined1)local_c40)));
          }
        }
        else {
          uVar21 = 0x30b;
          _snprintf_s(&local_b58,0x30c,0x30b,"%s");
        }
        FUN_18000aca0(local_c60,&local_b58);
        lVar12 = 0;
        do {
          lVar27 = lVar12 + 1;
          if (local_ce0[lVar12 + 0x1a4] !=
              *(CMyAlgorithmManager *)((longlong)&DAT_18002dacc + lVar12)) {
            FUN_18000ad00(&local_c40,L"<RootPath>");
            uVar25 = FUN_180023ad0(local_c60,(ushort *)&local_c40,uVar21);
            if (uVar25 != 0xffffffffffffffff) {
              uVar37 = FUN_180001c20((longlong)&local_c40);
              FUN_180023ab0((longlong *)local_c60,uVar25,uVar37,local_bc0);
            }
            _Filename = (wchar_t *)FUN_180001c10(local_c60);
            eVar9 = _waccess_s(_Filename,0);
            if (eVar9 == 0) goto LAB_180019e6a;
            cVar7 = HSlog_ShouldLog(4);
            if (cVar7 != '\0') {
              pWVar11 = (LPCWSTR)FUN_180001c10(local_c60);
              puVar16 = (undefined8 *)FUN_18000acd0(&local_c00,pWVar11);
              FUN_180001bd0(puVar16);
              puVar16 = FUN_180001320();
              puVar16 = FUN_180001bd0(puVar16);
              HSlog_LogPrint(4,"[%s] [ModuId:%d] [%s@%d] File %s is not exit",puVar16,
                             *(undefined4 *)(local_ce0 + 0x24938));
              FUN_180001a60(&local_c00);
            }
            FUN_180001be0(&local_c40);
            goto LAB_180019ee9;
          }
          lVar12 = lVar27;
        } while (lVar27 != 5);
        if (*(int *)pLVar26 < 2) {
          if (*(LPCSTR *)(local_cd0 + 0x610d8) != (LPCSTR)0x0) {
            FUN_18000aca0(&local_c40,*(LPCSTR *)(local_cd0 + 0x610d8));
            FUN_180023a90(&local_c40);
            FUN_180023a60((longlong *)local_c60,&local_c40);
            puVar16 = (undefined8 *)FUN_18000aca0(&local_c00,&local_b58);
            FUN_180023a80((longlong *)local_c60,puVar16);
            FUN_180001be0(&local_c00);
            pWVar11 = (LPCWSTR)FUN_180001c10(local_c60);
            puVar16 = (undefined8 *)FUN_18000acd0(&local_c00,pWVar11);
            FUN_180023b90((undefined8 *)local_cc8,puVar16);
            FUN_180001a60(&local_c00);
LAB_180019e6a:
            plVar28 = &local_c40;
            goto LAB_180019e6e;
          }
        }
        else {
          FUN_18000aca0(local_be0,&local_b58);
          lVar12 = FUN_18000add0(local_be0,0x5c);
          FUN_180001c30(local_be0,(longlong *)local_c20,lVar12 + 1,0xffffffffffffffff);
          lVar12 = FUN_18000add0(local_c20,0x2d);
          ppppppuVar15 = (undefined8 ******)
                         FUN_180001c30((longlong *)local_c20,&local_c40,lVar12 + 1,
                                       0xffffffffffffffff);
          if (local_c20 != ppppppuVar15) {
            if ((undefined8 *****)0x7 < local_c08) {
              operator_delete(local_c20[0]);
            }
            local_c08 = (undefined8 *****)0x7;
            local_c10 = (undefined8 *****)0x0;
            local_c20[0] = (undefined8 *****)((ulonglong)local_c20[0] & 0xffffffffffff0000);
            FUN_180001f00(local_c20,ppppppuVar15);
          }
          if (7 < local_c28) {
            operator_delete((void *)CONCAT62(local_c40._2_6_,
                                             CONCAT11(local_c40._1_1_,(undefined1)local_c40)));
          }
          FUN_18000aca0(&local_c00,*(LPCSTR *)(local_cd0 + 0x610d8));
          FUN_180025d40(&local_c00,&DAT_18002d840,1);
          FUN_180001f70((longlong *)local_c60,&local_c00,0,0xffffffffffffffff);
          ppppppwVar17 = local_c60;
          if (7 < local_c48) {
            ppppppwVar17 = (wchar_t ******)local_c60[0];
          }
          eVar9 = _waccess_s((wchar_t *)ppppppwVar17,0);
          if (eVar9 != 0) {
            ppppppwVar17 = local_c60;
            if (7 < local_c48) {
              ppppppwVar17 = (wchar_t ******)local_c60[0];
            }
            _wmkdir((wchar_t *)ppppppwVar17);
          }
          local_848._0_2_ = L'\0';
          memset((void *)((longlong)&local_848 + 2),0,0x7fe);
          pCVar20 = local_ce0;
          while( true ) {
            _snwprintf_s((wchar_t *)&local_848,0x3ff,0xffffffffffffffff,L"%s%d%d%d-%s");
            puVar16 = (undefined8 *)FUN_18000acd0(&local_c40,(LPCWSTR)&local_848);
            plVar28 = *(longlong **)(pCVar20 + 0x3b8);
            FUN_180027570((longlong *)local_c90,(longlong *)*plVar28,plVar28,puVar16);
            bVar44 = local_c90[0] == plVar28;
            if (0xf < local_c28) {
              operator_delete((void *)CONCAT62(local_c40._2_6_,
                                               CONCAT11(local_c40._1_1_,(undefined1)local_c40)));
            }
            if (bVar44) break;
            memset(&local_848,0,0x800);
          }
          if ((wchar_t)local_848 == L'\0') {
            uVar25 = 0;
          }
          else {
            uVar25 = 0xffffffffffffffff;
            do {
              uVar25 = uVar25 + 1;
            } while (*(short *)((longlong)&local_848 + uVar25 * 2) != 0);
          }
          FUN_180002350((ulonglong *)local_c60,&local_848,uVar25);
          ppppppwVar17 = local_c60;
          if (7 < local_c48) {
            ppppppwVar17 = (wchar_t ******)local_c60[0];
          }
          pFVar18 = _wfopen((wchar_t *)ppppppwVar17,L"wb+");
          pLVar35 = local_ca8;
          pLVar39 = local_cb0;
          iVar8 = local_cd8;
          if (pFVar18 == (FILE *)0x0) {
            cVar7 = HSlog_ShouldLog(4);
            if (cVar7 != '\0') {
              ppppppwVar17 = local_c60;
              if (7 < local_c48) {
                ppppppwVar17 = (wchar_t ******)local_c60[0];
              }
              FUN_18000acd0(&local_c40,(LPCWSTR)ppppppwVar17);
              puVar16 = FUN_180001320();
              if (0xf < (ulonglong)puVar16[3]) {
                puVar16 = (undefined8 *)*puVar16;
              }
              uVar21 = 4;
              HSlog_LogPrint(4,"[%s] [ModuId:%d] [%s@%d] File %s open error",puVar16,
                             *(undefined4 *)(local_ce0 + 0x24938));
              if (0xf < local_c28) {
                FUN_180002340(uVar21,(void *)CONCAT62(local_c40._2_6_,
                                                      CONCAT11(local_c40._1_1_,(undefined1)local_c40
                                                              )));
              }
              local_c28 = 0xf;
              FUN_180001e10(&local_c40,0);
            }
            FUN_180001be0(&local_c00);
            FUN_180001be0(local_c20);
            FUN_180001be0(local_be0);
          }
          else {
            fwrite(*(void **)(pLVar26 + -8),(longlong)*(int *)pLVar26,1,pFVar18);
            fflush(pFVar18);
            fclose(pFVar18);
            ppppppwVar17 = local_c60;
            if (7 < local_c48) {
              ppppppwVar17 = (wchar_t ******)local_c60[0];
            }
            puVar16 = (undefined8 *)FUN_18000acd0(&local_c40,(LPCWSTR)ppppppwVar17);
            FUN_180023b90((undefined8 *)local_cc8,puVar16);
            if (0xf < local_c28) {
              operator_delete((void *)CONCAT62(local_c40._2_6_,
                                               CONCAT11(local_c40._1_1_,(undefined1)local_c40)));
            }
            FUN_180001be0(&local_c00);
            FUN_180001be0(local_c20);
            plVar28 = local_be0;
LAB_180019e6e:
            FUN_180001be0(plVar28);
            pWVar11 = (LPCWSTR)FUN_180001c10(local_c60);
            FUN_18000acd0(&local_b98,pWVar11);
            pcVar22 = (char *)FUN_180001bd0(&local_b98);
            pCVar20 = local_ce0;
            AddImage2CircleList(local_ce0,pcVar22);
            lVar12 = *(longlong *)pCVar20;
            uVar25 = FUN_180001c20((longlong)&local_b98);
            puVar16 = FUN_180001bd0(&local_b98);
            (**(code **)(lVar12 + 0x18))(local_ce0,"CurrentImagePath",puVar16,uVar25 & 0xffffffff);
            FUN_180001a60(&local_b98);
          }
        }
LAB_180019ee9:
        FUN_180001be0(local_c60);
      }
      local_cd8 = iVar8 + 1;
      pLVar32 = pLVar32 + 0x20;
      pLVar26 = pLVar26 + 0x118;
    } while (local_cd8 < *(int *)pLVar39);
  }
LAB_180019f16:
  pCVar20 = local_ce0;
  pvVar40 = (void *)0x0;
  if (*(int *)(local_ce0 + 0x259ac) == -1) goto LAB_18001a0ff;
  local_ce8[0] = 0xffffffff;
  iVar8 = VM_M_GetModuleMapping(*(longlong *)(local_ce0 + 8),0,0,local_ce8);
  uVar34 = local_ce8[0];
  if (iVar8 == -0x1fffffee) {
    auVar4._8_8_ = 0;
    auVar4._0_8_ = (longlong)(int)local_ce8[0];
    _Var23 = SUB168(ZEXT816(0x88) * auVar4,0);
    if (SUB168(ZEXT816(0x88) * auVar4,8) != 0) {
      _Var23 = 0xffffffffffffffff;
    }
    _Dst = (void *)thunk_FUN_180028f10(_Var23);
    if (_Dst == (void *)0x0) goto LAB_18001a0ff;
    memset(_Dst,0,(longlong)(int)local_ce8[0] * 0x88);
    iVar8 = VM_M_GetModuleMapping(*(longlong *)(pCVar20 + 8),_Dst,uVar34,local_ce8);
    if (iVar8 == 0) goto LAB_180019fb3;
  }
  else {
    _Dst = pvVar40;
    if (iVar8 != 0) goto LAB_18001a0ff;
LAB_180019fb3:
    FUN_180008800(local_c90);
    if ((void *)(longlong)(int)local_ce8[0] != (void *)0x0) {
      piVar24 = (int *)((longlong)_Dst + 4);
LAB_180019fd5:
      if (*piVar24 != *(int *)(pCVar20 + 0x259ac)) goto code_r0x000180019fd9;
      cVar7 = HSlog_ShouldLog(4);
      if (cVar7 != '\0') {
        puVar16 = FUN_180001320();
        puVar16 = FUN_180001bd0(puVar16);
        HSlog_LogPrint(4,"[%s] [ModuId:%d] [%s@%d] find the map[%d]",puVar16,
                       *(undefined4 *)(pCVar20 + 0x24938));
      }
      *(undefined4 *)(pCVar20 + 0x259ac) =
           *(undefined4 *)((longlong)pvVar40 * 0x88 + (longlong)_Dst);
      local_b98._0_1_ = '\0';
      local_b98._1_7_ = 0;
      uStack_b90 = 0;
      local_b8f = 0;
      uStack_b87 = 0;
      local_b80 = 0;
      uStack_b7f = 0;
      uStack_b78 = 0;
      local_b77 = 0;
      local_b6f = 0;
      local_b67 = 0;
      local_b5f = 0;
      local_b5b = 0;
      local_b59 = 0;
      sprintf((char *)&local_b98,"%d");
      lVar12 = -1;
      do {
        lVar43 = lVar12 + 1;
        lVar27 = lVar12 + 1;
        lVar12 = lVar43;
      } while (*(char *)((longlong)&local_b98 + lVar27) != '\0');
      (**(code **)(*(longlong *)pCVar20 + 0x18))(pCVar20,"CameraID",&local_b98,lVar43);
    }
LAB_18001a0e7:
    FUN_180006f80((longlong *)local_c90);
    if (_Dst == (void *)0x0) goto LAB_18001a0ff;
  }
  operator_delete__(_Dst);
LAB_18001a0ff:
  thunk_FUN_180025bc0(&local_c80);
  thunk_FUN_180025bc0(&local_ca8);
  FUN_180001be0(local_bc0);
  return 0;
code_r0x00018001881d:
  uVar41 = uVar36 + 1;
  if (uVar36 + 1 == 8) goto code_r0x000180018823;
  goto LAB_180018810;
code_r0x000180018823:
  memcpy_s(local_cb8,(ulonglong)((int)uVar36 - 3),*(void **)pLVar35,(longlong)*(int *)(pLVar35 + 8))
  ;
LAB_180018964:
  uVar34 = (int)uVar37 + 1;
  uVar37 = (ulonglong)uVar34;
  pLVar35 = pLVar35 + 0x118;
  if (*(int *)pLVar39 <= (int)uVar34) goto code_r0x000180018977;
  goto LAB_180018790;
code_r0x000180018977:
  param_1 = local_cd0;
  if (local_cb8[0] != 0x1a4) goto LAB_1800197a9;
  local_cd8 = 0;
  local_c90[0] = (longlong *)((longlong)pLStack_ca0 - (longlong)local_ca8 >> 5);
  if (local_c90[0] != (longlong *)0x0) {
    local_cc0 = (LOAD_MODULE_DATA_PARAM *)0x0;
    pCVar20 = local_ce0;
LAB_1800189c0:
    pLVar35 = local_cc0;
    pLVar39 = (LOAD_MODULE_DATA_PARAM *)0x0;
    pCVar42 = (CMyAlgorithmManager *)(local_ca8 + (longlong)local_cc0 * 0x20);
    if (0xf < *(ulonglong *)(pCVar42 + 0x18)) {
      pCVar42 = *(CMyAlgorithmManager **)pCVar42;
    }
    local_b58 = '\0';
    local_cc8 = pCVar42;
    memset(local_b57,0,0x30b);
    pLVar32 = (LOAD_MODULE_DATA_PARAM *)0x1;
    lVar12 = -1;
    do {
      lVar12 = lVar12 + 1;
    } while (pCVar42[lVar12] != (CMyAlgorithmManager)0x0);
    pLVar26 = pLVar39;
    pCVar38 = pCVar42;
    pLVar33 = pLVar39;
    if (lVar12 != 0) {
LAB_180018a30:
      iVar8 = (int)pLVar26;
      CVar1 = *pCVar38;
      if (((byte)CVar1 & 0x80) != 0) {
        pLVar32 = pLVar39;
      }
      if (iVar8 == 0) {
        if ((byte)CVar1 < 0x80) goto LAB_180018a90;
        if ((byte)((char)CVar1 + 4U) < 2) {
          iVar8 = 6;
        }
        else if ((byte)CVar1 < 0xf8) {
          if ((byte)CVar1 < 0xf0) {
            if ((byte)CVar1 < 0xe0) {
              if (0xbf < (byte)CVar1) {
                iVar8 = 2;
                goto LAB_180018a8e;
              }
              goto LAB_180018ac2;
            }
            iVar8 = 3;
          }
          else {
            iVar8 = 4;
          }
        }
        else {
          iVar8 = 5;
        }
      }
      else if (((byte)CVar1 & 0xc0) != 0x80) goto LAB_180018ac2;
LAB_180018a8e:
      pLVar26 = (LOAD_MODULE_DATA_PARAM *)(ulonglong)(iVar8 - 1);
LAB_180018a90:
      uVar34 = (int)pLVar33 + 1;
      uVar25 = 0xffffffffffffffff;
      do {
        uVar25 = uVar25 + 1;
      } while (pCVar42[uVar25] != (CMyAlgorithmManager)0x0);
      pCVar38 = pCVar38 + 1;
      pLVar33 = (LOAD_MODULE_DATA_PARAM *)(ulonglong)uVar34;
      if (uVar25 <= (ulonglong)(longlong)(int)uVar34) goto code_r0x000180018ab6;
      goto LAB_180018a30;
    }
    goto LAB_180018ac2;
  }
  goto LAB_180019f16;
code_r0x000180018ab6:
  if (((int)pLVar26 != 0) || (bVar44 = true, (char)pLVar32 != '\0')) {
LAB_180018ac2:
    bVar44 = false;
  }
  if (bVar44) {
    _snprintf_s(&local_b58,0x30c,0x30b,"%s");
  }
  else {
    FUN_18000aba0(&local_b98,(LPCSTR)pCVar42);
    _snprintf_s(&local_b58,0x30c,0x30b,"%s");
    if (0xf < CONCAT71(uStack_b7f,local_b80)) {
      operator_delete((void *)CONCAT71(local_b98._1_7_,(char)local_b98));
    }
  }
  FUN_18000aca0(local_c60,&local_b58);
  pLVar32 = pLVar39;
  do {
    pLVar26 = pLVar32 + 1;
    if ((pCVar20 + 0x1a4)[(longlong)pLVar32] != *(CMyAlgorithmManager *)(pLVar32 + 0x18002dacc)) {
      local_be8 = 7;
      local_bf0 = 0;
      local_c00._0_1_ = 0;
      local_c00._1_1_ = 0;
      uVar21 = 10;
      FUN_180002350(&local_c00,L"<RootPath>",10);
      uVar25 = local_bf0;
      puVar31 = (ushort *)&local_c00;
      if (7 < local_be8) {
        puVar31 = (ushort *)
                  CONCAT62(local_c00._2_6_,CONCAT11(local_c00._1_1_,(undefined1)local_c00));
      }
      uVar37 = FUN_180002060(local_c60,puVar31,uVar21,local_bf0);
      if (uVar37 != 0xffffffffffffffff) {
        FUN_180002680((longlong *)local_c60,uVar37,uVar25,local_bc0,0,0xffffffffffffffff);
      }
      ppppppwVar17 = local_c60;
      if (7 < local_c48) {
        ppppppwVar17 = (wchar_t ******)local_c60[0];
      }
      iVar8 = _waccess((wchar_t *)ppppppwVar17,0);
      if (iVar8 == 0) {
        pLVar39 = pLVar35;
        if (7 < local_be8) {
          operator_delete((void *)CONCAT62(local_c00._2_6_,
                                           CONCAT11(local_c00._1_1_,(undefined1)local_c00)));
        }
        goto LAB_180019611;
      }
      cVar7 = HSlog_ShouldLog(4);
      if (cVar7 != '\0') {
        ppppppwVar17 = local_c60;
        if (7 < local_c48) {
          ppppppwVar17 = (wchar_t ******)local_c60[0];
        }
        FUN_18000acd0(&local_b98,(LPCWSTR)ppppppwVar17);
        if ((DAT_18003cf00 & 1) == 0) {
          DAT_18003cf00 = DAT_18003cf00 | 1;
          FUN_180001360();
          atexit(FUN_18002c980);
        }
        puVar16 = &DAT_18003cee0;
        if (0xf < DAT_18003cef8) {
          puVar16 = DAT_18003cee0;
        }
        HSlog_LogPrint(4,"[%s] [ModuId:%d] [%s@%d] File %s is not exit",puVar16,
                       *(undefined4 *)(pCVar20 + 0x24938));
        if (0xf < CONCAT71(uStack_b7f,local_b80)) {
          operator_delete((void *)CONCAT71(local_b98._1_7_,(char)local_b98));
        }
      }
      if (7 < local_be8) {
        operator_delete((void *)CONCAT62(local_c00._2_6_,
                                         CONCAT11(local_c00._1_1_,(undefined1)local_c00)));
      }
      local_be8 = 7;
      local_bf0 = 0;
      local_c00._0_1_ = 0;
      local_c00._1_1_ = 0;
      pLVar39 = pLVar35;
      if (7 < local_c48) {
        operator_delete(local_c60[0]);
      }
      goto LAB_180019736;
    }
    pLVar32 = pLVar26;
  } while (pLVar26 != (LOAD_MODULE_DATA_PARAM *)0x5);
  local_cb0 = local_cd0 + (longlong)pLVar35 * 0x118;
  if (*(int *)(local_cb0 + 0x585a8) < 2) {
    if (*(LPCSTR *)(local_cd0 + 0x610d8) == (LPCSTR)0x0) {
      pLVar39 = pLVar35;
      if (7 < local_c48) {
        operator_delete(local_c60[0]);
      }
      goto LAB_180019736;
    }
    FUN_18000aca0(&local_c40,*(LPCSTR *)(local_cd0 + 0x610d8));
    FUN_180025d40(&local_c40,&DAT_18002d840,1);
    FUN_180001f70((longlong *)local_c60,&local_c40,0,0xffffffffffffffff);
    puVar16 = (undefined8 *)FUN_18000aca0(&local_b98,&local_b58);
    FUN_180025c50((longlong *)local_c60,puVar16,0,0xffffffffffffffff);
    if (7 < CONCAT71(uStack_b7f,local_b80)) {
      operator_delete((void *)CONCAT71(local_b98._1_7_,(char)local_b98));
    }
    ppppppwVar17 = local_c60;
    if (7 < local_c48) {
      ppppppwVar17 = (wchar_t ******)local_c60[0];
    }
    puVar16 = (undefined8 *)FUN_18000acd0(&local_b98,(LPCWSTR)ppppppwVar17);
    FUN_180023b90((undefined8 *)(pCVar20 + 0x3b8),puVar16);
    if (0xf < CONCAT71(uStack_b7f,local_b80)) {
      operator_delete((void *)CONCAT71(local_b98._1_7_,(char)local_b98));
    }
    pLVar39 = pLVar35;
    if (7 < local_c28) {
      operator_delete((void *)CONCAT62(local_c40._2_6_,
                                       CONCAT11(local_c40._1_1_,(undefined1)local_c40)));
    }
LAB_180019611:
    uVar25 = 0;
    ppppppwVar17 = local_c60;
    if (7 < local_c48) {
      ppppppwVar17 = (wchar_t ******)local_c60[0];
    }
    FUN_18000acd0(local_be0,(LPCWSTR)ppppppwVar17);
    plVar28 = local_be0;
    if (0xf < local_bc8) {
      plVar28 = (longlong *)CONCAT71(local_be0[0]._1_7_,(undefined1)local_be0[0]);
    }
    AddImage2CircleList(pCVar20,(char *)plVar28);
    plVar28 = local_be0;
    if (0xf < local_bc8) {
      plVar28 = (longlong *)CONCAT71(local_be0[0]._1_7_,(undefined1)local_be0[0]);
    }
    (**(code **)(*(longlong *)pCVar20 + 0x18))
              (pCVar20,"CurrentImagePath",plVar28,local_bd0 & 0xffffffff);
    uVar41 = lStack_c78 - (longlong)local_c80 >> 5;
    puVar16 = local_c80;
    uVar37 = uVar25;
    if (uVar41 != 0) {
      do {
        if (uVar41 <= uVar37) goto LAB_180019796;
        puVar13 = puVar16;
        if (0xf < (ulonglong)puVar16[3]) {
          puVar13 = (undefined8 *)*puVar16;
        }
        pCVar20 = pCVar42;
        do {
          CVar1 = *pCVar20;
          CVar2 = pCVar20[(longlong)puVar13 - (longlong)pCVar42];
          if (CVar1 != CVar2) break;
          pCVar20 = pCVar20 + 1;
        } while (CVar2 != (CMyAlgorithmManager)0x0);
        if (CVar1 == CVar2) {
          plVar28 = local_be0;
          if (0xf < local_bc8) {
            plVar28 = (longlong *)CONCAT71(local_be0[0]._1_7_,(undefined1)local_be0[0]);
          }
          (**(code **)(*(longlong *)local_ce0 + 0x18))
                    (local_ce0,"SelectExecutePic",plVar28,local_bd0 & 0xffffffff);
        }
        uVar34 = (int)uVar25 + 1;
        uVar25 = (ulonglong)uVar34;
        puVar16 = puVar16 + 4;
        uVar37 = uVar37 + 1;
      } while ((ulonglong)(longlong)(int)uVar34 < uVar41);
    }
    if (0xf < local_bc8) {
      operator_delete((void *)CONCAT71(local_be0[0]._1_7_,(undefined1)local_be0[0]));
    }
    local_bc8 = 0xf;
    local_bd0 = 0;
    local_be0[0]._0_1_ = 0;
    pCVar20 = local_ce0;
    if (7 < local_c48) {
      operator_delete(local_c60[0]);
      pCVar20 = local_ce0;
    }
LAB_180019736:
    local_cd8 = local_cd8 + 1;
    local_cc0 = pLVar39 + 1;
    if (local_c90[0] <= (longlong *)(longlong)local_cd8) goto LAB_180019f16;
    goto LAB_1800189c0;
  }
  FUN_18000aca0(&local_c00,&local_b58);
  uVar25 = local_be8;
  local_ce8[0] = CONCAT22(local_ce8[0]._2_2_,0x5c);
  if (local_bf0 != 0) {
    puVar16 = (undefined8 *)
              CONCAT62(local_c00._2_6_,CONCAT11(local_c00._1_1_,(undefined1)local_c00));
    puVar13 = &local_c00;
    if (7 < local_be8) {
      puVar13 = puVar16;
    }
    puVar31 = (ushort *)((longlong)puVar13 + (local_bf0 - 1) * 2);
    do {
      if (*puVar31 == 0x5c) {
        lVar12 = 1;
        puVar30 = local_ce8;
        puVar14 = puVar31;
        while (*puVar14 == (ushort)*puVar30) {
          puVar14 = puVar14 + 1;
          puVar30 = (uint *)((longlong)puVar30 + 2);
          lVar12 = lVar12 + -1;
          if (lVar12 == 0) goto LAB_180018c32;
        }
        iVar8 = 1;
        if (*puVar14 < (ushort)*puVar30) {
          iVar8 = -1;
        }
        if (iVar8 == 0) {
LAB_180018c32:
          puVar13 = &local_c00;
          if (7 < uVar25) {
            puVar13 = puVar16;
          }
          lVar12 = (longlong)puVar31 - (longlong)puVar13 >> 1;
          goto LAB_180018c49;
        }
      }
      puVar14 = (ushort *)FUN_180001c10(&local_c00);
      if (puVar31 == puVar14) break;
      puVar31 = puVar31 + -1;
    } while( true );
  }
  lVar12 = -1;
LAB_180018c49:
  FUN_180001c30(&local_c00,(longlong *)local_c20,lVar12 + 1,0xffffffffffffffff);
  ppppuVar6 = local_c08;
  pppppuVar5 = local_c20[0];
  local_ce8[0] = CONCAT22(local_ce8[0]._2_2_,0x2d);
  if ((undefined8 *****)local_c10 != (undefined8 *****)0x0) {
    ppppppuVar15 = local_c20;
    if ((undefined8 *****)0x7 < local_c08) {
      ppppppuVar15 = (undefined8 ******)local_c20[0];
    }
    puVar31 = (ushort *)((longlong)ppppppuVar15 + ((longlong)local_c10 + -1) * 2);
    do {
      if (*puVar31 == 0x2d) {
        lVar12 = 1;
        puVar30 = local_ce8;
        puVar14 = puVar31;
        while (*puVar14 == (ushort)*puVar30) {
          puVar14 = puVar14 + 1;
          puVar30 = (uint *)((longlong)puVar30 + 2);
          lVar12 = lVar12 + -1;
          if (lVar12 == 0) goto LAB_180018cf0;
        }
        iVar8 = 1;
        if (*puVar14 < (ushort)*puVar30) {
          iVar8 = -1;
        }
        if (iVar8 == 0) {
LAB_180018cf0:
          ppppppuVar15 = local_c20;
          if ((undefined8 *****)0x7 < ppppuVar6) {
            ppppppuVar15 = (undefined8 ******)pppppuVar5;
          }
          lVar12 = (longlong)puVar31 - (longlong)ppppppuVar15 >> 1;
          goto LAB_180018d07;
        }
      }
      puVar14 = (ushort *)FUN_180001c10(local_c20);
      if (puVar31 == puVar14) break;
      puVar31 = puVar31 + -1;
    } while( true );
  }
  lVar12 = -1;
LAB_180018d07:
  ppppppuVar15 = (undefined8 ******)
                 FUN_180001c30((longlong *)local_c20,&local_b98,lVar12 + 1,0xffffffffffffffff);
  if (local_c20 != ppppppuVar15) {
    if ((undefined8 *****)0x7 < local_c08) {
      operator_delete(local_c20[0]);
    }
    local_c08 = (undefined8 ****)0x7;
    local_c10 = (undefined8 ****)0x0;
    local_c20[0] = (undefined8 *****)((ulonglong)local_c20[0] & 0xffffffffffff0000);
    if (ppppppuVar15[3] < (undefined8 *****)0x8) {
      if ((longlong)ppppppuVar15[2] + 1 != 0) {
        memmove(local_c20,ppppppuVar15,((longlong)ppppppuVar15[2] + 1) * 2);
      }
    }
    else {
      local_c20[0] = *ppppppuVar15;
      *ppppppuVar15 = (undefined8 *****)0x0;
    }
    local_c10 = ppppppuVar15[2];
    local_c08 = ppppppuVar15[3];
    ppppppuVar15[3] = (undefined8 *****)0x7;
    ppppppuVar15[2] = (undefined8 *****)0x0;
    *(undefined2 *)ppppppuVar15 = 0;
  }
  if (7 < CONCAT71(uStack_b7f,local_b80)) {
    operator_delete((void *)CONCAT71(local_b98._1_7_,(char)local_b98));
  }
  FUN_18000aca0(&local_c40,*(LPCSTR *)(local_cd0 + 0x610d8));
  FUN_180025d40(&local_c40,&DAT_18002d840,1);
  FUN_180001f70((longlong *)local_c60,&local_c40,0,0xffffffffffffffff);
  ppppppwVar17 = local_c60;
  if (7 < local_c48) {
    ppppppwVar17 = (wchar_t ******)local_c60[0];
  }
  eVar9 = _waccess_s((wchar_t *)ppppppwVar17,0);
  if (eVar9 != 0) {
    ppppppwVar17 = local_c60;
    if (7 < local_c48) {
      ppppppwVar17 = (wchar_t ******)local_c60[0];
    }
    _wmkdir((wchar_t *)ppppppwVar17);
  }
  local_ce8[0] = 0;
  local_848._0_2_ = L'\0';
  memset((void *)((longlong)&local_848 + 2),0,0x7fe);
  pCVar20 = local_ce0;
  while( true ) {
    _snwprintf_s((wchar_t *)&local_848,0x3ff,0xffffffffffffffff,L"%s%d%d%d-%s");
    puVar16 = (undefined8 *)FUN_18000acd0(&local_b98,(LPCWSTR)&local_848);
    plVar28 = *(longlong **)(pCVar20 + 0x3b8);
    plVar29 = (longlong *)*plVar28;
    bVar44 = plVar29 == plVar28;
    if (!bVar44) {
      uVar25 = puVar16[3];
      uVar37 = puVar16[2];
      do {
        _Buf1 = plVar29 + 2;
        puVar13 = puVar16;
        if (0xf < uVar25) {
          puVar13 = (undefined8 *)*puVar16;
        }
        uVar41 = plVar29[4];
        if (0xf < (ulonglong)plVar29[5]) {
          _Buf1 = (longlong *)*_Buf1;
        }
        uVar36 = uVar37;
        if (uVar41 < uVar37) {
          uVar36 = uVar41;
        }
        if (uVar36 == 0) {
          uVar34 = 0;
        }
        else {
          uVar34 = memcmp(_Buf1,puVar13,uVar36);
        }
        if (uVar34 == 0) {
          if (uVar41 < uVar37) {
            uVar34 = 0xffffffff;
          }
          else {
            uVar34 = (uint)(uVar41 != uVar37);
          }
        }
      } while ((uVar34 != 0) && (plVar29 = (longlong *)*plVar29, plVar29 != plVar28));
      pLVar39 = (LOAD_MODULE_DATA_PARAM *)(ulonglong)local_ce8[0];
      bVar44 = plVar29 == plVar28;
      pCVar20 = local_ce0;
    }
    if (0xf < CONCAT71(uStack_b7f,local_b80)) {
      operator_delete((void *)CONCAT71(local_b98._1_7_,(char)local_b98));
    }
    if (bVar44) break;
    local_ce8[0] = (int)pLVar39 + 1;
    pLVar39 = (LOAD_MODULE_DATA_PARAM *)(ulonglong)local_ce8[0];
    memset(&local_848,0,0x800);
  }
  if ((wchar_t)local_848 == L'\0') {
    uVar25 = 0;
  }
  else {
    uVar25 = 0xffffffffffffffff;
    do {
      uVar25 = uVar25 + 1;
    } while (*(short *)((longlong)&local_848 + uVar25 * 2) != 0);
  }
  ppppppwVar17 = local_c60;
  if (7 < local_c48) {
    ppppppwVar17 = (wchar_t ******)local_c60[0];
  }
  if (&local_848 < ppppppwVar17) {
LAB_18001906e:
    if (0x7ffffffffffffffe < uVar25) goto LAB_180019788;
    if (local_c48 < uVar25) {
      FUN_180002560(local_c60,uVar25,local_c50);
    }
    else if (uVar25 == 0) {
      local_c50 = 0;
      ppppppwVar17 = local_c60;
      if (7 < local_c48) {
        ppppppwVar17 = (wchar_t ******)local_c60[0];
      }
      *(wchar_t *)ppppppwVar17 = L'\0';
    }
    if (uVar25 != 0) {
      ppppppwVar17 = local_c60;
      if (7 < local_c48) {
        ppppppwVar17 = (wchar_t ******)local_c60[0];
      }
      if (uVar25 != 0) {
        memcpy(ppppppwVar17,&local_848,uVar25 * 2);
      }
      ppppppwVar17 = local_c60;
      if (7 < local_c48) {
        ppppppwVar17 = (wchar_t ******)local_c60[0];
      }
      local_c50 = uVar25;
      *(wchar_t *)((longlong)ppppppwVar17 + uVar25 * 2) = L'\0';
    }
LAB_180019108:
    ppppppwVar17 = local_c60;
    if (7 < local_c48) {
      ppppppwVar17 = (wchar_t ******)local_c60[0];
    }
    pFVar18 = _wfopen((wchar_t *)ppppppwVar17,L"wb+");
    if (pFVar18 == (FILE *)0x0) {
      cVar7 = HSlog_ShouldLog(4);
      if (cVar7 != '\0') {
        ppppppwVar17 = local_c60;
        if (7 < local_c48) {
          ppppppwVar17 = (wchar_t ******)local_c60[0];
        }
        FUN_18000acd0(&local_b98,(LPCWSTR)ppppppwVar17);
        if ((DAT_18003cf00 & 1) == 0) {
          DAT_18003cf00 = DAT_18003cf00 | 1;
          FUN_180001360();
          atexit(FUN_18002c980);
        }
        puVar16 = &DAT_18003cee0;
        if (0xf < DAT_18003cef8) {
          puVar16 = DAT_18003cee0;
        }
        HSlog_LogPrint(4,"[%s] [ModuId:%d] [%s@%d] File %s open error",puVar16,
                       *(undefined4 *)(pCVar20 + 0x24938));
        if (0xf < CONCAT71(uStack_b7f,local_b80)) {
          operator_delete((void *)CONCAT71(local_b98._1_7_,(char)local_b98));
        }
      }
      if (7 < local_c28) {
        operator_delete((void *)CONCAT62(local_c40._2_6_,
                                         CONCAT11(local_c40._1_1_,(undefined1)local_c40)));
      }
      local_c28 = 7;
      local_c30 = 0;
      local_c40._0_1_ = 0;
      local_c40._1_1_ = 0;
      if ((undefined8 *****)0x7 < local_c08) {
        operator_delete(local_c20[0]);
      }
      local_c08 = (undefined8 *****)0x7;
      local_c10 = (undefined8 *****)0x0;
      local_c20[0] = (undefined8 *****)((ulonglong)local_c20[0] & 0xffffffffffff0000);
      if (7 < local_be8) {
        operator_delete((void *)CONCAT62(local_c00._2_6_,
                                         CONCAT11(local_c00._1_1_,(undefined1)local_c00)));
      }
      local_be8 = 7;
      local_bf0 = 0;
      local_c00._0_1_ = 0;
      local_c00._1_1_ = 0;
      pLVar39 = local_cc0;
      if (7 < local_c48) {
        operator_delete(local_c60[0]);
        pLVar39 = local_cc0;
      }
      goto LAB_180019736;
    }
    fwrite(*(void **)(local_cb0 + 0x585a0),(longlong)*(int *)(local_cb0 + 0x585a8),1,pFVar18);
    fflush(pFVar18);
    fclose(pFVar18);
    ppppppwVar17 = local_c60;
    if (7 < local_c48) {
      ppppppwVar17 = (wchar_t ******)local_c60[0];
    }
    puVar16 = &local_b98;
    puVar19 = (undefined8 *)FUN_18000acd0(puVar16,(LPCWSTR)ppppppwVar17);
    puVar13 = *(undefined8 **)(pCVar20 + 0x3b8);
    lVar12 = FUN_1800270f0(puVar16,puVar13,(undefined8 *)puVar13[1],puVar19);
    if (*(longlong *)(pCVar20 + 0x3c0) != 0x555555555555554) {
      *(longlong *)(pCVar20 + 0x3c0) = *(longlong *)(pCVar20 + 0x3c0) + 1;
      puVar13[1] = lVar12;
      **(longlong **)(lVar12 + 8) = lVar12;
      if (0xf < CONCAT71(uStack_b7f,local_b80)) {
        operator_delete((void *)CONCAT71(local_b98._1_7_,(char)local_b98));
      }
      if (7 < local_c28) {
        operator_delete((void *)CONCAT62(local_c40._2_6_,
                                         CONCAT11(local_c40._1_1_,(undefined1)local_c40)));
      }
      local_c28 = 7;
      local_c30 = 0;
      local_c40._0_1_ = 0;
      local_c40._1_1_ = 0;
      if ((undefined8 *****)0x7 < local_c08) {
        operator_delete(local_c20[0]);
      }
      local_c08 = (undefined8 *****)0x7;
      local_c10 = (undefined8 *****)0x0;
      local_c20[0] = (undefined8 *****)((ulonglong)local_c20[0] & 0xffffffffffff0000);
      pLVar39 = local_cc0;
      pCVar42 = local_cc8;
      if (7 < local_be8) {
        operator_delete((void *)CONCAT62(local_c00._2_6_,
                                         CONCAT11(local_c00._1_1_,(undefined1)local_c00)));
        pLVar39 = local_cc0;
        pCVar42 = local_cc8;
      }
      goto LAB_180019611;
    }
  }
  else {
    ppppppwVar17 = local_c60;
    if (7 < local_c48) {
      ppppppwVar17 = (wchar_t ******)local_c60[0];
    }
    if ((wchar_t *)((longlong)ppppppwVar17 + local_c50 * 2) <= &local_848) goto LAB_18001906e;
    ppppppwVar17 = local_c60;
    if (7 < local_c48) {
      ppppppwVar17 = (wchar_t ******)local_c60[0];
    }
    uVar37 = (longlong)&local_848 - (longlong)ppppppwVar17 >> 1;
    if (uVar37 <= local_c50) {
      uVar41 = local_c50 - uVar37;
      if (uVar25 < local_c50 - uVar37) {
        uVar41 = uVar25;
      }
      uVar41 = uVar41 + uVar37;
      if (local_c50 < uVar41) {
        std::_Xout_of_range("invalid string position");
        pcVar3 = (code *)swi(3);
        iVar8 = (*pcVar3)();
        return iVar8;
      }
      ppppppwVar17 = local_c60;
      if (7 < local_c48) {
        ppppppwVar17 = (wchar_t ******)local_c60[0];
      }
      local_c50 = uVar41;
      *(wchar_t *)((longlong)ppppppwVar17 + uVar41 * 2) = L'\0';
      FUN_180002430(local_c60,0,uVar37);
      goto LAB_180019108;
    }
    std::_Xout_of_range("invalid string position");
  }
  std::_Xlength_error("list<T> too long");
LAB_180019788:
  std::_Xlength_error("string too long");
LAB_180019796:
  std::_Xout_of_range("invalid vector<T> subscript");
  param_1 = local_cd0;
  goto LAB_1800197a9;
code_r0x000180019fd9:
  pvVar40 = (void *)((longlong)pvVar40 + 1);
  piVar24 = piVar24 + 0x22;
  if ((void *)(longlong)(int)local_ce8[0] <= pvVar40) goto LAB_18001a0e7;
  goto LAB_180019fd5;
}

