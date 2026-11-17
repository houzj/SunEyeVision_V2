
/* private: int __cdecl CIMVSDivideImageModu::ProcessAlg(struct
   CIMVSDivideImageModu::_DIVIDE_IMAGE_MODU_INPUT * __ptr64,struct
   CIMVSDivideImageModu::_DIVIDE_IMAGE_MODU_OUTPUT * __ptr64) __ptr64 */

int __thiscall
CIMVSDivideImageModu::ProcessAlg
          (CIMVSDivideImageModu *this,_DIVIDE_IMAGE_MODU_INPUT *param_1,
          _DIVIDE_IMAGE_MODU_OUTPUT *param_2)

{
  float fVar1;
  float fVar2;
  float fVar3;
  float fVar4;
  undefined1 auVar5 [16];
  undefined1 auVar6 [16];
  undefined1 auVar7 [16];
  undefined1 auVar8 [16];
  undefined1 auVar9 [16];
  char cVar10;
  __uint64 _Var11;
  undefined8 uVar12;
  void *_Dst;
  undefined8 *puVar13;
  int iVar14;
  ulonglong uVar15;
  int iVar16;
  int iVar17;
  longlong lVar18;
  longlong lVar19;
  double dVar20;
  double dVar21;
  float fVar22;
  float fVar23;
  float fVar24;
  float fVar25;
  float fVar26;
  float fVar27;
  float fVar28;
  float fVar29;
  undefined8 in_stack_fffffffffffffef8;
  undefined4 uVar30;
  
                    /* 0x1f510  12
                       ?ProcessAlg@CIMVSDivideImageModu@@AEAAHPEAU_DIVIDE_IMAGE_MODU_INPUT@1@PEAU_DIVIDE_IMAGE_MODU_OUTPUT@1@@Z
                        */
  uVar30 = (undefined4)((ulonglong)in_stack_fffffffffffffef8 >> 0x20);
  if ((param_1 == (_DIVIDE_IMAGE_MODU_INPUT *)0x0) || (param_2 == (_DIVIDE_IMAGE_MODU_OUTPUT *)0x0))
  {
    cVar10 = HSlog_ShouldLog(4);
    if (cVar10 != '\0') {
      if ((DAT_180031d04 & 1) == 0) {
        DAT_180031d04 = DAT_180031d04 | 1;
        FUN_1800056a0();
        atexit(FUN_180025b20);
      }
      puVar13 = &DAT_180031d60;
      if (0xf < DAT_180031d78) {
        puVar13 = DAT_180031d60;
      }
      HSlog_LogPrint(4,"[%s] [ModuId:%d] [%s@%d] %s return [%x]",puVar13,
                     *(undefined4 *)(this + 0x34),"CIMVSDivideImageModu::ProcessAlg",0x121,
                     "ProcessAlg",0xe0000001);
    }
    iVar14 = -0x1fffffff;
  }
  else {
    FUN_18001a330();
    iVar14 = *(int *)(this + 0x15d90);
    if (((((iVar14 < 1) || (iVar16 = *(int *)(this + 0x15d94), iVar16 < 1)) || (100 < iVar14)) ||
        ((100 < iVar16 || (100 < *(uint *)(this + 0x15d98))))) || (100 < *(uint *)(this + 0x15d9c)))
    {
      cVar10 = HSlog_ShouldLog(4);
      if (cVar10 != '\0') {
        if ((DAT_180031d04 & 1) == 0) {
          DAT_180031d04 = DAT_180031d04 | 1;
          FUN_1800056a0();
          atexit(FUN_180025b20);
        }
        puVar13 = &DAT_180031d60;
        if (0xf < DAT_180031d78) {
          puVar13 = DAT_180031d60;
        }
        HSlog_LogPrint(4,
                       "[%s] [ModuId:%d] [%s@%d] Param Range Error!NumX:[%d],NumY:[%d],OverlaprateX:[%d],OverlaprateY:[%d]."
                       ,puVar13,*(undefined4 *)(this + 0x34),"CIMVSDivideImageModu::ProcessAlg",
                       0x12f,CONCAT44(uVar30,*(undefined4 *)(this + 0x15d90)),
                       *(undefined4 *)(this + 0x15d94),*(undefined4 *)(this + 0x15d98),
                       *(undefined4 *)(this + 0x15d9c));
      }
      iVar14 = -0x1fffffff;
    }
    else {
      DeleteModuBuf(this,param_2);
      uVar15 = (ulonglong)(iVar16 * iVar14);
      auVar5._8_8_ = 0;
      auVar5._0_8_ = uVar15;
      _Var11 = SUB168(ZEXT816(4) * auVar5,0);
      if (SUB168(ZEXT816(4) * auVar5,8) != 0) {
        _Var11 = 0xffffffffffffffff;
      }
      uVar12 = thunk_FUN_1800205f4(_Var11);
      *(undefined8 *)(param_2 + 0x10) = uVar12;
      auVar6._8_8_ = 0;
      auVar6._0_8_ = uVar15;
      _Var11 = SUB168(ZEXT816(4) * auVar6,0);
      if (SUB168(ZEXT816(4) * auVar6,8) != 0) {
        _Var11 = 0xffffffffffffffff;
      }
      uVar12 = thunk_FUN_1800205f4(_Var11);
      *(undefined8 *)(param_2 + 0x18) = uVar12;
      auVar7._8_8_ = 0;
      auVar7._0_8_ = uVar15;
      _Var11 = SUB168(ZEXT816(4) * auVar7,0);
      if (SUB168(ZEXT816(4) * auVar7,8) != 0) {
        _Var11 = 0xffffffffffffffff;
      }
      uVar12 = thunk_FUN_1800205f4(_Var11);
      *(undefined8 *)(param_2 + 8) = uVar12;
      auVar8._8_8_ = 0;
      auVar8._0_8_ = uVar15;
      _Var11 = SUB168(ZEXT816(4) * auVar8,0);
      if (SUB168(ZEXT816(4) * auVar8,8) != 0) {
        _Var11 = 0xffffffffffffffff;
      }
      uVar12 = thunk_FUN_1800205f4(_Var11);
      *(undefined8 *)param_2 = uVar12;
      auVar9._8_8_ = 0;
      auVar9._0_8_ = uVar15;
      _Var11 = SUB168(ZEXT816(4) * auVar9,0);
      if (SUB168(ZEXT816(4) * auVar9,8) != 0) {
        _Var11 = 0xffffffffffffffff;
      }
      _Dst = (void *)thunk_FUN_1800205f4(_Var11);
      *(void **)(param_2 + 0x20) = _Dst;
      if (((*(longlong *)(param_2 + 0x10) == 0) || (*(longlong *)(param_2 + 0x18) == 0)) ||
         ((*(longlong *)(param_2 + 8) == 0 || ((*(longlong *)param_2 == 0 || (_Dst == (void *)0x0)))
          ))) {
        cVar10 = HSlog_ShouldLog(4);
        if (cVar10 != '\0') {
          puVar13 = FUN_180005660();
          if (0xf < (ulonglong)puVar13[3]) {
            puVar13 = (undefined8 *)*puVar13;
          }
          HSlog_LogPrint(4,"[%s] [ModuId:%d] [%s@%d] RESOURCE CREATE FAIL.",puVar13,
                         *(undefined4 *)(this + 0x34),"CIMVSDivideImageModu::ProcessAlg",0x13f);
        }
        DeleteModuBuf(this,param_2);
        iVar14 = -0x1ffffffd;
      }
      else {
        memset(_Dst,0,uVar15 * 4);
        fVar1 = *(float *)(param_1 + 0x4c);
        fVar2 = *(float *)(param_1 + 0x44);
        fVar3 = *(float *)(param_1 + 0x50);
        fVar4 = *(float *)(param_1 + 0x48);
        fVar28 = *(float *)(param_1 + 0x54);
        dVar20 = cos((double)fVar28 * 0.017453292519943295);
        dVar21 = sin((double)fVar28 * 0.017453292519943295);
        iVar14 = *(int *)(this + 0x15d90);
        fVar28 = fVar1 / (float)iVar14;
        fVar29 = fVar3 / (float)*(int *)(this + 0x15d94);
        iVar16 = 0;
        if (0 < *(int *)(this + 0x15d94)) {
          lVar18 = 0;
          do {
            iVar17 = 0;
            lVar19 = 0;
            if (0 < iVar14) {
              do {
                fVar22 = *(float *)(param_1 + 0x50);
                fVar25 = (fVar22 - fVar29) * ((float)*(int *)(this + 0x15d9c) / 100.0) + fVar29;
                fVar26 = (fVar25 - fVar22 * ((float)*(int *)(this + 0x15d9c) / 100.0)) *
                         (float)iVar16;
                fVar23 = *(float *)(param_1 + 0x4c);
                fVar24 = (fVar23 - fVar28) * ((float)*(int *)(this + 0x15d98) / 100.0) + fVar28;
                fVar27 = (fVar24 - fVar23 * ((float)*(int *)(this + 0x15d98) / 100.0)) *
                         (float)iVar17;
                if (iVar17 == 0) {
                  fVar27 = 0.0;
                }
                if (iVar16 == 0) {
                  fVar26 = 0.0;
                }
                if (fVar22 < fVar25) {
                  fVar25 = fVar22;
                }
                if (fVar23 < fVar24) {
                  fVar24 = fVar23;
                }
                fVar23 = (fVar24 * 0.5 + fVar27 + (fVar2 - fVar1 * 0.5)) -
                         *(float *)(param_1 + 0x44);
                fVar22 = (fVar25 * 0.5 + fVar26 + (fVar4 - fVar3 * 0.5)) -
                         *(float *)(param_1 + 0x48);
                *(float *)(*(longlong *)(param_2 + 0x10) +
                          (*(int *)(this + 0x15d90) * lVar18 + lVar19) * 4) =
                     (fVar23 * (float)dVar20 - fVar22 * (float)dVar21) + *(float *)(param_1 + 0x44);
                *(float *)(*(longlong *)(param_2 + 0x18) +
                          (*(int *)(this + 0x15d90) * lVar18 + lVar19) * 4) =
                     fVar22 * (float)dVar20 + fVar23 * (float)dVar21 + *(float *)(param_1 + 0x48);
                *(float *)(*(longlong *)(param_2 + 8) +
                          (*(int *)(this + 0x15d90) * lVar18 + lVar19) * 4) = fVar25;
                *(float *)(*(longlong *)param_2 + (*(int *)(this + 0x15d90) * lVar18 + lVar19) * 4)
                     = fVar24;
                *(undefined4 *)
                 (*(longlong *)(param_2 + 0x20) + (*(int *)(this + 0x15d90) * lVar18 + lVar19) * 4)
                     = *(undefined4 *)(param_1 + 0x54);
                iVar17 = iVar17 + 1;
                lVar19 = lVar19 + 1;
                iVar14 = *(int *)(this + 0x15d90);
              } while (iVar17 < iVar14);
            }
            iVar16 = iVar16 + 1;
            lVar18 = lVar18 + 1;
          } while (iVar16 < *(int *)(this + 0x15d94));
        }
        iVar14 = 0;
      }
    }
  }
  return iVar14;
}

