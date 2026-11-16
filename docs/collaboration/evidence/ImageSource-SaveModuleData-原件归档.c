
/* WARNING: Function: __security_check_cookie replaced with injection: security_check_cookie */
/* WARNING: Removing unreachable block (ram,0x000180017608) */
/* WARNING: Type propagation algorithm not settling */
/* public: virtual int __cdecl CMyAlgorithmManager::SaveModuleData(struct SAVE_MODULE_DATA_PARAM *
   __ptr64) __ptr64 */

int __thiscall
CMyAlgorithmManager::SaveModuleData(CMyAlgorithmManager *this,SAVE_MODULE_DATA_PARAM *param_1)

{
  char cVar1;
  longlong lVar2;
  int iVar3;
  char cVar4;
  int iVar5;
  int iVar6;
  CMyAlgorithmManager *pCVar7;
  CMyAlgorithmManager *pCVar8;
  char *pcVar9;
  SAVE_MODULE_DATA_PARAM *pSVar10;
  char *pcVar11;
  longlong *plVar12;
  wchar_t *_Filename;
  undefined8 *puVar13;
  void *_Buf1;
  undefined1 *puVar14;
  longlong lVar15;
  LPCWSTR ******pppppppWVar16;
  int iVar17;
  SAVE_MODULE_DATA_PARAM *pSVar18;
  ulonglong uVar19;
  char *pcVar20;
  undefined8 **ppuVar21;
  longlong lVar22;
  ulonglong uVar23;
  longlong lVar24;
  SAVE_MODULE_DATA_PARAM *pSVar25;
  undefined1 auStackY_968 [32];
  int local_918;
  longlong local_8e8 [2];
  char *local_8d8;
  ulonglong local_8d0;
  ulonglong local_8c8 [2];
  char *local_8b8;
  ulonglong local_8b0;
  SAVE_MODULE_DATA_PARAM local_8a8;
  undefined7 uStack_8a7;
  char *local_898;
  ulonglong local_890;
  LPCWSTR ******local_888 [3];
  ulonglong local_870;
  void *local_868 [3];
  ulonglong local_850;
  char local_848 [1024];
  char local_448;
  undefined1 local_447 [1023];
  ulonglong local_48;
  
                    /* 0x171a0  36
                       ?SaveModuleData@CMyAlgorithmManager@@UEAAHPEAUSAVE_MODULE_DATA_PARAM@@@Z */
  local_48 = DAT_18003c000 ^ (ulonglong)auStackY_968;
  if (param_1 == (SAVE_MODULE_DATA_PARAM *)0x0) {
    iVar5 = -0x1fffffff;
  }
  else {
    if (*(longlong *)(param_1 + 0x610d8) == 0) {
      cVar4 = HSlog_ShouldLog(4);
      if (cVar4 != '\0') {
        if ((DAT_18003cf00 & 1) == 0) {
          DAT_18003cf00 = DAT_18003cf00 | 1;
          FUN_180001360();
          atexit(FUN_18002c980);
        }
        ppuVar21 = &DAT_18003cee0;
        if (0xf < DAT_18003cef8) {
          ppuVar21 = (undefined8 **)DAT_18003cee0;
        }
        HSlog_LogPrint(4,
                       "[%s] [ModuId:%d] [%s@%d] pstSaveInfo->struModuleData.pszAttachmentsDir == NULL"
                       ,ppuVar21,*(undefined4 *)(this + 0x24938));
      }
    }
    else {
      cVar4 = HSlog_ShouldLog(4);
      if (cVar4 != '\0') {
        if ((DAT_18003cf00 & 1) == 0) {
          DAT_18003cf00 = DAT_18003cf00 | 1;
          FUN_180001360();
          atexit(FUN_18002c980);
        }
        ppuVar21 = &DAT_18003cee0;
        if (0xf < DAT_18003cef8) {
          ppuVar21 = (undefined8 **)DAT_18003cee0;
        }
        HSlog_LogPrint(4,
                       "[%s] [ModuId:%d] [%s@%d] pstSaveInfo->struModuleData.pszAttachmentsDir == %s\r\n"
                       ,ppuVar21,*(undefined4 *)(this + 0x24938));
      }
    }
    CAbstractUserModule::SaveModuleData((CAbstractUserModule *)this,param_1);
    *(code **)(param_1 + 0x61118) = FUN_18000c9d0;
    *(CMyAlgorithmManager **)(param_1 + 0x61120) = this;
    *(undefined4 *)(param_1 + 0x58488) = 1;
    *(undefined4 *)(param_1 + 0x58490) = 0;
    GetRootPath(this);
    if ((LPCRITICAL_SECTION)(this + 0x248e8) != (LPCRITICAL_SECTION)0x0) {
      EnterCriticalSection((LPCRITICAL_SECTION)(this + 0x248e8));
    }
    local_918 = 0;
    lVar24 = *(longlong *)(this + 0x248d8);
    lVar2 = *(longlong *)(this + 0x248d0);
    pCVar7 = this + 0x25a68;
    *(undefined8 *)(this + 0x25a78) = 0;
    if (0xf < *(ulonglong *)(this + 0x25a80)) {
      pCVar7 = *(CMyAlgorithmManager **)pCVar7;
    }
    *pCVar7 = (CMyAlgorithmManager)0x0;
    pCVar7 = this + 0x25a88;
    *(undefined8 *)(this + 0x25a98) = 0;
    pCVar8 = pCVar7;
    if (0xf < *(ulonglong *)(this + 0x25aa0)) {
      pCVar8 = *(CMyAlgorithmManager **)pCVar7;
    }
    *pCVar8 = (CMyAlgorithmManager)0x0;
    pSVar25 = param_1;
    if (*(int *)(param_1 + 0x58490) < 0x78) {
LAB_1800173dd:
      pCVar8 = this + 0x25a68;
      pcVar9 = (char *)FUN_18000b430((longlong)(this + 0x248c0),local_918);
      pSVar25 = param_1;
      if (pcVar9 != (char *)0x0) {
        local_8b0 = 0xf;
        local_8b8 = (char *)0x0;
        local_8c8[0]._0_1_ = '\0';
        if (*pcVar9 == '\0') {
          uVar19 = 0;
        }
        else {
          uVar19 = 0xffffffffffffffff;
          do {
            uVar19 = uVar19 + 1;
          } while (pcVar9[uVar19] != '\0');
        }
        FUN_180001aa0(local_8c8,pcVar9,uVar19);
        lVar22 = 0;
        do {
          lVar15 = lVar22 + 1;
          iVar5 = (int)((lVar24 - lVar2) / 0x310);
          if (this[lVar22 + 0x1a4] != *(CMyAlgorithmManager *)((longlong)&DAT_18002dacc + lVar22)) {
            pppppppWVar16 = (LPCWSTR ******)local_888;
            if (7 < local_870) {
              pppppppWVar16 = local_888[0];
            }
            FUN_18000acd0(&local_8a8,(LPCWSTR)pppppppWVar16);
            pcVar9 = local_898;
            pSVar25 = &local_8a8;
            if (0xf < local_890) {
              pSVar25 = (SAVE_MODULE_DATA_PARAM *)CONCAT71(uStack_8a7,local_8a8);
            }
            if (local_898 == (char *)0x0) {
              uVar19 = 0;
              goto LAB_180017c9a;
            }
            if ((local_8b8 == (char *)0x0) || (local_8b8 < local_898)) goto LAB_180017cc9;
            pcVar20 = local_8b8 + (1 - (longlong)local_898);
            puVar14 = (undefined1 *)local_8c8;
            if (0xf < local_8b0) {
              puVar14 = (undefined1 *)CONCAT71(local_8c8[0]._1_7_,(char)local_8c8[0]);
            }
            goto LAB_180017c30;
          }
          lVar22 = lVar15;
        } while (lVar15 != 5);
        local_890 = 0xf;
        local_898 = (char *)0x0;
        local_8a8 = (SAVE_MODULE_DATA_PARAM)0x0;
        if (*(longlong *)(param_1 + 0x610d8) == 0) {
          FUN_180001ce0((longlong *)&local_8a8,(longlong *)local_8c8,0,0xffffffffffffffff);
          SaveImageFileToSol(this,&local_8a8);
          FUN_180024ce0((longlong *)pCVar8,local_8c8,0,0xffffffffffffffff);
          FUN_180024e00((ulonglong *)pCVar8,&DAT_18002f7fc,1);
          iVar17 = 0;
          iVar3 = (int)(*(longlong *)(this + 0x24888) - *(longlong *)(this + 0x24880) >> 0x3f);
          iVar6 = (int)((*(longlong *)(this + 0x24888) - *(longlong *)(this + 0x24880)) / 0x310) +
                  iVar3;
          if (iVar6 != iVar3 && -1 < iVar6 - iVar3) {
            do {
              if (((*(longlong *)(this + 0x24880) != *(longlong *)(this + 0x24888)) &&
                  (iVar17 < (int)((*(longlong *)(this + 0x24888) - *(longlong *)(this + 0x24880)) /
                                 0x310))) && (-1 < iVar17)) {
                if ((ulonglong)
                    ((*(longlong *)(this + 0x24888) - *(longlong *)(this + 0x24880)) / 0x310) <=
                    (ulonglong)(longlong)iVar17) {
                  std::_Xout_of_range("invalid vector<T> subscript");
                  goto LAB_180017f20;
                }
                pcVar9 = (char *)((longlong)iVar17 * 0x310 + *(longlong *)(this + 0x24880));
                if (pcVar9 != (char *)0x0) {
                  puVar14 = (undefined1 *)local_8c8;
                  if (0xf < local_8b0) {
                    puVar14 = (undefined1 *)CONCAT71(local_8c8[0]._1_7_,(char)local_8c8[0]);
                  }
                  lVar22 = (longlong)puVar14 - (longlong)pcVar9;
                  do {
                    cVar4 = *pcVar9;
                    cVar1 = pcVar9[lVar22];
                    if (cVar4 != cVar1) break;
                    pcVar9 = pcVar9 + 1;
                  } while (cVar1 != '\0');
                  if (cVar4 == cVar1) {
                    FUN_180024ce0((longlong *)pCVar7,local_8c8,0,0xffffffffffffffff);
                    FUN_180024e00((ulonglong *)pCVar7,&DAT_18002f7fc,1);
                  }
                }
              }
              iVar17 = iVar17 + 1;
            } while (iVar17 < (int)((*(longlong *)(this + 0x24888) - *(longlong *)(this + 0x24880))
                                   / 0x310));
          }
          goto LAB_180017ed0;
        }
        FUN_180001ce0((longlong *)&local_8a8,(longlong *)local_8c8,0,0xffffffffffffffff);
        uVar19 = local_890;
        if (local_898 == (char *)0x0) {
LAB_180017654:
          lVar22 = -1;
        }
        else {
          pSVar25 = (SAVE_MODULE_DATA_PARAM *)CONCAT71(uStack_8a7,local_8a8);
          pSVar18 = &local_8a8;
          if (0xf < local_890) {
            pSVar18 = pSVar25;
          }
          for (pSVar18 = pSVar18 + (longlong)(local_898 + -1);
              *pSVar18 != (SAVE_MODULE_DATA_PARAM)0x5c; pSVar18 = pSVar18 + -1) {
            pSVar10 = (SAVE_MODULE_DATA_PARAM *)FUN_180001bd0((undefined8 *)&local_8a8);
            if (pSVar18 == pSVar10) goto LAB_180017654;
          }
          pSVar10 = &local_8a8;
          if (0xf < uVar19) {
            pSVar10 = pSVar25;
          }
          lVar22 = (longlong)pSVar18 - (longlong)pSVar10;
        }
        FUN_180023a20((longlong *)&local_8a8,local_8e8,lVar22 + 1,0xffffffffffffffff);
        if ((local_8d8 != (char *)0x0) && (local_8d8 != (char *)0x0)) {
          pcVar9 = (char *)local_8e8;
          pcVar20 = local_8d8;
          if (0xf < local_8d0) {
            pcVar9 = (char *)CONCAT71(local_8e8[0]._1_7_,(char)local_8e8[0]);
          }
          while ((pcVar20 != (char *)0x0 &&
                 (pcVar11 = memchr(pcVar9,0x2d,(size_t)pcVar20), pcVar11 != (char *)0x0))) {
            if (*pcVar11 == '-') {
              puVar14 = (undefined1 *)local_8e8;
              if (0xf < local_8d0) {
                puVar14 = (undefined1 *)CONCAT71(local_8e8[0]._1_7_,(char)local_8e8[0]);
              }
              lVar22 = (longlong)pcVar11 - (longlong)puVar14;
              goto LAB_1800176d9;
            }
            pcVar20 = pcVar9 + (longlong)(pcVar20 + (-1 - (longlong)pcVar11));
            pcVar9 = pcVar11 + 1;
          }
        }
        lVar22 = -1;
LAB_1800176d9:
        plVar12 = FUN_180023a20(local_8e8,(longlong *)local_868,lVar22 + 1,0xffffffffffffffff);
        if (local_8e8 != plVar12) {
          if (0xf < local_8d0) {
            operator_delete((void *)CONCAT71(local_8e8[0]._1_7_,(char)local_8e8[0]));
          }
          local_8d0 = 0xf;
          local_8d8 = (char *)0x0;
          local_8e8[0]._0_1_ = '\0';
          FUN_180001c70(local_8e8,plVar12);
        }
        pcVar9 = (char *)0x0;
        if (0xf < local_850) {
          operator_delete(local_868[0]);
        }
        local_448 = '\0';
        memset(local_447,0,0x3ff);
        sprintf(&local_448,"%s\\%d%d%d-%s",*(undefined8 *)(param_1 + 0x610d8),
                (ulonglong)*(uint *)(this + 0x24938));
        while( true ) {
          _Filename = (wchar_t *)FUN_18000aca0(local_868,&local_448);
          if (7 < *(ulonglong *)(_Filename + 0xc)) {
            _Filename = *(wchar_t **)_Filename;
          }
          iVar6 = _waccess(_Filename,0);
          if (7 < local_850) {
            operator_delete(local_868[0]);
          }
          if (iVar6 == -1) break;
          memset(&local_448,0,0x400);
          _snprintf_s(&local_448,0x400,0x3ff,"%s\\%d%d%d-%s");
        }
        pcVar20 = (char *)local_8c8;
        if (0xf < local_8b0) {
          pcVar20 = (char *)CONCAT71(local_8c8[0]._1_7_,(char)local_8c8[0]);
        }
        iVar6 = CopyFileFunction(this,pcVar20,&local_448);
        if (iVar6 == 0) {
          local_848[0] = '\0';
          memset(local_848 + 1,0,0x3ff);
          _snprintf_s(local_848,0x400,0x3ff,"%d%d%d-%s");
          if ((this + 0x3e9 != (CMyAlgorithmManager *)0x0) && (*(int *)(param_1 + 0x58490) < 0x80))
          {
            lVar22 = (longlong)*(int *)(param_1 + 0x58490) * 0x118;
            *(undefined4 *)(param_1 + lVar22 + 0x585a8) = 1;
            *(CMyAlgorithmManager **)(param_1 + lVar22 + 0x585a0) = this + 0x3e9;
            _snprintf_s((char *)(param_1 + lVar22 + 0x58498),0x104,0x103,"%s");
            *(int *)(param_1 + 0x58490) = *(int *)(param_1 + 0x58490) + 1;
          }
          if (local_848[0] == '\0') {
            uVar23 = 0;
          }
          else {
            uVar19 = 0xffffffffffffffff;
            do {
              uVar23 = uVar19 + 1;
              lVar22 = uVar19 + 1;
              uVar19 = uVar23;
            } while (local_848[lVar22] != '\0');
          }
          FUN_180024e00((ulonglong *)pCVar8,local_848,uVar23);
          FUN_180024e00((ulonglong *)pCVar8,&DAT_18002f7fc,1);
          iVar17 = 0;
          iVar3 = (int)(*(longlong *)(this + 0x24888) - *(longlong *)(this + 0x24880) >> 0x3f);
          iVar6 = (int)((*(longlong *)(this + 0x24888) - *(longlong *)(this + 0x24880)) / 0x310) +
                  iVar3;
          if (iVar6 != iVar3 && -1 < iVar6 - iVar3) {
            do {
              if (((*(longlong *)(this + 0x24880) == *(longlong *)(this + 0x24888)) ||
                  ((int)((*(longlong *)(this + 0x24888) - *(longlong *)(this + 0x24880)) / 0x310) <=
                   iVar17)) || (iVar17 < 0)) {
                pcVar9 = (char *)0x0;
              }
              else {
                if ((ulonglong)
                    ((*(longlong *)(this + 0x24888) - *(longlong *)(this + 0x24880)) / 0x310) <=
                    (ulonglong)(longlong)iVar17) goto LAB_180017f20;
                pcVar9 = (char *)((longlong)iVar17 * 0x310 + *(longlong *)(this + 0x24880));
              }
              puVar14 = (undefined1 *)local_8c8;
              if (0xf < local_8b0) {
                puVar14 = (undefined1 *)CONCAT71(local_8c8[0]._1_7_,(char)local_8c8[0]);
              }
              lVar22 = (longlong)puVar14 - (longlong)pcVar9;
              do {
                cVar4 = *pcVar9;
                cVar1 = pcVar9[lVar22];
                if (cVar4 != cVar1) break;
                pcVar9 = pcVar9 + 1;
              } while (cVar1 != '\0');
              if (cVar4 == cVar1) {
                if (local_848[0] == '\0') {
                  uVar23 = 0;
                }
                else {
                  uVar19 = 0xffffffffffffffff;
                  do {
                    uVar23 = uVar19 + 1;
                    lVar22 = uVar19 + 1;
                    uVar19 = uVar23;
                  } while (local_848[lVar22] != '\0');
                }
                FUN_180024e00((ulonglong *)pCVar7,local_848,uVar23);
                FUN_180024e00((ulonglong *)pCVar7,&DAT_18002f7fc,1);
              }
              iVar17 = iVar17 + 1;
            } while (iVar17 < (int)((*(longlong *)(this + 0x24888) - *(longlong *)(this + 0x24880))
                                   / 0x310));
          }
          if (0xf < local_8d0) {
            operator_delete((void *)CONCAT71(local_8e8[0]._1_7_,(char)local_8e8[0]));
          }
          local_8d0 = 0xf;
          local_8d8 = (char *)0x0;
          local_8e8[0]._0_1_ = '\0';
          if (0xf < local_890) {
            operator_delete((void *)CONCAT71(uStack_8a7,local_8a8));
          }
          goto LAB_180017ed0;
        }
        cVar4 = HSlog_ShouldLog(4);
        if (cVar4 != '\0') {
          puVar13 = FUN_180001320();
          if (0xf < (ulonglong)puVar13[3]) {
            puVar13 = (undefined8 *)*puVar13;
          }
          HSlog_LogPrint(4,"[%s] [ModuId:%d] [%s@%d] nRet = %d",puVar13,
                         *(undefined4 *)(this + 0x24938));
        }
        local_918 = local_918 + 1;
        if (iVar5 <= local_918) goto LAB_180017f2e;
        if (0xf < local_8d0) {
          operator_delete((void *)CONCAT71(local_8e8[0]._1_7_,(char)local_8e8[0]));
        }
        local_8d0 = 0xf;
        local_8d8 = (char *)0x0;
        local_8e8[0]._0_1_ = '\0';
        if (0xf < local_890) {
          operator_delete((void *)CONCAT71(uStack_8a7,local_8a8));
        }
        local_890 = 0xf;
        local_898 = (char *)0x0;
        local_8a8 = (SAVE_MODULE_DATA_PARAM)0x0;
        goto LAB_180017ee4;
      }
    }
LAB_180017f98:
    pCVar8 = this + 0x25a68;
    lVar24 = (longlong)*(int *)(pSVar25 + 0x58490) * 0x118;
    *(int *)(pSVar25 + lVar24 + 0x585a8) = (int)*(undefined8 *)(this + 0x25a78) + 1;
    if (0xf < *(ulonglong *)(this + 0x25a80)) {
      pCVar8 = *(CMyAlgorithmManager **)pCVar8;
    }
    *(CMyAlgorithmManager **)(pSVar25 + lVar24 + 0x585a0) = pCVar8;
    _snprintf_s((char *)(pSVar25 + lVar24 + 0x58498),0x104,0x103,"%s");
    *(int *)(pSVar25 + 0x58490) = *(int *)(pSVar25 + 0x58490) + 1;
    lVar24 = (longlong)*(int *)(pSVar25 + 0x58490) * 0x118;
    *(int *)(pSVar25 + lVar24 + 0x585a8) = (int)*(undefined8 *)(this + 0x25a98) + 1;
    if (0xf < *(ulonglong *)(this + 0x25aa0)) {
      pCVar7 = *(CMyAlgorithmManager **)pCVar7;
    }
    *(CMyAlgorithmManager **)(pSVar25 + lVar24 + 0x585a0) = pCVar7;
    _snprintf_s((char *)(pSVar25 + lVar24 + 0x58498),0x104,0x103,"%s");
    *(int *)(pSVar25 + 0x58490) = *(int *)(pSVar25 + 0x58490) + 1;
    memcpy_s(pSVar25 + (longlong)*(int *)(pSVar25 + 0x58490) * 0x118 + 0x58498,0x104,"DynamicInData"
             ,0xe);
    *(undefined4 *)(pSVar25 + ((longlong)*(int *)(pSVar25 + 0x58490) + 1) * 0x118 + 0x58490) =
         *(undefined4 *)(this + 0x25ae0);
    *(undefined8 *)(pSVar25 + (longlong)*(int *)(pSVar25 + 0x58490) * 0x118 + 0x585a0) =
         *(undefined8 *)(this + 0x25ad8);
    *(int *)(pSVar25 + 0x58490) = *(int *)(pSVar25 + 0x58490) + 1;
    memcpy_s(pSVar25 + (longlong)*(int *)(pSVar25 + 0x58490) * 0x118 + 0x58498,0x104,
             "DynamicOutData",0xf);
    *(undefined4 *)(pSVar25 + ((longlong)*(int *)(pSVar25 + 0x58490) + 1) * 0x118 + 0x58490) =
         *(undefined4 *)(this + 0x25af0);
    *(undefined8 *)(pSVar25 + (longlong)*(int *)(pSVar25 + 0x58490) * 0x118 + 0x585a0) =
         *(undefined8 *)(this + 0x25ae8);
    *(int *)(pSVar25 + 0x58490) = *(int *)(pSVar25 + 0x58490) + 1;
    memcpy_s(pSVar25 + (longlong)*(int *)(pSVar25 + 0x58490) * 0x118 + 0x58498,0x104,"Version",8);
    *(undefined4 *)(pSVar25 + ((longlong)*(int *)(pSVar25 + 0x58490) + 1) * 0x118 + 0x58490) = 4;
    *(CMyAlgorithmManager **)(pSVar25 + (longlong)*(int *)(pSVar25 + 0x58490) * 0x118 + 0x585a0) =
         this + 0x25c10;
    *(int *)(pSVar25 + 0x58490) = *(int *)(pSVar25 + 0x58490) + 1;
    if ((LPCRITICAL_SECTION)(this + 0x248e8) != (LPCRITICAL_SECTION)0x0) {
      LeaveCriticalSection((LPCRITICAL_SECTION)(this + 0x248e8));
    }
    if (7 < local_870) {
      operator_delete(local_888[0]);
    }
    iVar5 = 0;
  }
  return iVar5;
LAB_180017c30:
  if ((pcVar20 == (char *)0x0) ||
     (_Buf1 = memchr(puVar14,(int)(char)*pSVar25,(size_t)pcVar20), _Buf1 == (void *)0x0))
  goto LAB_180017cc9;
  iVar6 = memcmp(_Buf1,pSVar25,(size_t)pcVar9);
  if (iVar6 != 0) {
    pcVar20 = puVar14 + (longlong)(pcVar20 + (-1 - (longlong)_Buf1));
    puVar14 = (undefined1 *)((longlong)_Buf1 + 1);
    goto LAB_180017c30;
  }
  puVar14 = (undefined1 *)local_8c8;
  if (0xf < local_8b0) {
    puVar14 = (undefined1 *)CONCAT71(local_8c8[0]._1_7_,(char)local_8c8[0]);
  }
  uVar19 = (longlong)_Buf1 - (longlong)puVar14;
  if (uVar19 != 0xffffffffffffffff) {
LAB_180017c9a:
    FUN_1800252d0(local_8c8,uVar19,(ulonglong)local_898,"<RootPath>",10);
  }
LAB_180017cc9:
  FUN_180024ce0((longlong *)(this + 0x25a68),local_8c8,0,0xffffffffffffffff);
  FUN_180024e00((ulonglong *)(this + 0x25a68),&DAT_18002f7fc,1);
  iVar6 = 0;
  if (0 < (int)((*(longlong *)(this + 0x24888) - *(longlong *)(this + 0x24880)) / 0x310)) {
    do {
      if (((*(longlong *)(this + 0x24880) == *(longlong *)(this + 0x24888)) ||
          ((int)((*(longlong *)(this + 0x24888) - *(longlong *)(this + 0x24880)) / 0x310) <= iVar6))
         || (iVar6 < 0)) {
LAB_180017e08:
        cVar4 = HSlog_ShouldLog(4);
        if (cVar4 != '\0') {
          if ((DAT_18003cf00 & 1) == 0) {
            DAT_18003cf00 = DAT_18003cf00 | 1;
            FUN_180001360();
            atexit(FUN_18002c980);
          }
          ppuVar21 = &DAT_18003cee0;
          if (0xf < DAT_18003cef8) {
            ppuVar21 = (undefined8 **)DAT_18003cee0;
          }
          HSlog_LogPrint(4,"[%s] [ModuId:%d] [%s@%d] GetNodeByIndex is null.",ppuVar21,
                         *(undefined4 *)(this + 0x24938));
        }
        local_918 = local_918 + 1;
        if (iVar5 <= local_918) break;
      }
      else {
        if ((ulonglong)((*(longlong *)(this + 0x24888) - *(longlong *)(this + 0x24880)) / 0x310) <=
            (ulonglong)(longlong)iVar6) {
          std::_Xout_of_range("invalid vector<T> subscript");
          goto LAB_180017f80;
        }
        pcVar9 = (char *)((longlong)iVar6 * 0x310 + *(longlong *)(this + 0x24880));
        if (pcVar9 == (char *)0x0) goto LAB_180017e08;
        puVar14 = (undefined1 *)local_8c8;
        if (0xf < local_8b0) {
          puVar14 = (undefined1 *)CONCAT71(local_8c8[0]._1_7_,(char)local_8c8[0]);
        }
        lVar22 = (longlong)puVar14 - (longlong)pcVar9;
        do {
          cVar4 = *pcVar9;
          cVar1 = pcVar9[lVar22];
          if (cVar4 != cVar1) break;
          pcVar9 = pcVar9 + 1;
        } while (cVar1 != '\0');
        if (cVar4 == cVar1) {
          FUN_180024ce0((longlong *)pCVar7,local_8c8,0,0xffffffffffffffff);
          FUN_180024e00((ulonglong *)pCVar7,&DAT_18002f7fc,1);
        }
      }
      iVar6 = iVar6 + 1;
    } while (iVar6 < (int)((*(longlong *)(this + 0x24888) - *(longlong *)(this + 0x24880)) / 0x310))
    ;
  }
  if (0xf < local_890) {
    operator_delete((void *)CONCAT71(uStack_8a7,local_8a8));
  }
LAB_180017ed0:
  local_918 = local_918 + 1;
  pSVar25 = param_1;
  if (local_918 < iVar5) {
LAB_180017ee4:
    if (0xf < local_8b0) {
      operator_delete((void *)CONCAT71(local_8c8[0]._1_7_,(char)local_8c8[0]));
    }
    pSVar25 = param_1;
    if (0x77 < *(int *)(param_1 + 0x58490)) goto LAB_180017f98;
    goto LAB_1800173dd;
  }
LAB_180017f80:
  if (0xf < local_8b0) {
    operator_delete((void *)CONCAT71(local_8c8[0]._1_7_,(char)local_8c8[0]));
  }
  goto LAB_180017f98;
LAB_180017f20:
  pcVar9 = (char *)0x5397829cbc14e5e1;
  std::_Xout_of_range("invalid vector<T> subscript");
LAB_180017f2e:
  if (0xf < local_8d0) {
    operator_delete((void *)CONCAT71(local_8e8[0]._1_7_,(char)local_8e8[0]));
  }
  local_8d0 = 0xf;
  local_8e8[0]._0_1_ = '\0';
  local_8d8 = pcVar9;
  if (0xf < local_890) {
    operator_delete((void *)CONCAT71(uStack_8a7,local_8a8));
  }
  local_890 = 0xf;
  local_898 = pcVar9;
  local_8a8 = (SAVE_MODULE_DATA_PARAM)0x0;
  pSVar25 = param_1;
  goto LAB_180017f80;
}

