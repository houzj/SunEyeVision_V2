
/* public: int __cdecl CMyAlgorithmManager::StitchImageOutput(void * __ptr64,struct
   CAMERA_IMAGE_DATA,struct CAMERA_IMAGE_DATA & __ptr64) __ptr64 */

int __thiscall
CMyAlgorithmManager::StitchImageOutput
          (CMyAlgorithmManager *this,void *param_1,int *param_3,CAMERA_IMAGE_DATA *param_4)

{
  CMyAlgorithmManager *pCVar1;
  double dVar2;
  double dVar3;
  double dVar4;
  double dVar5;
  double dVar6;
  double dVar7;
  undefined4 uVar8;
  CAMERA_IMAGE_DATA *pCVar9;
  char cVar10;
  int iVar11;
  int iVar12;
  undefined8 *puVar13;
  char *pcVar14;
  int iVar15;
  int iVar16;
  undefined4 uVar17;
  double dVar18;
  undefined4 uVar20;
  undefined4 uVar21;
  double dVar19;
  LARGE_INTEGER local_res8;
  void *local_res10;
  LARGE_INTEGER local_res18;
  CAMERA_IMAGE_DATA *local_res20;
  undefined4 uVar22;
  undefined8 in_stack_fffffffffffffdf8;
  undefined8 uVar23;
  undefined8 in_stack_fffffffffffffe00;
  undefined8 uVar24;
  LARGE_INTEGER local_198;
  double local_190;
  double local_188;
  double local_180;
  undefined8 local_178;
  undefined1 local_160 [24];
  double local_148;
  double local_140;
  undefined8 local_138;
  undefined8 local_130;
  
  uVar22 = (undefined4)((ulonglong)in_stack_fffffffffffffdf8 >> 0x20);
                    /* 0x20980  44
                       ?StitchImageOutput@CMyAlgorithmManager@@QEAAHPEAXUCAMERA_IMAGE_DATA@@AEAU2@@Z
                        */
  uVar17 = (undefined4)((ulonglong)in_stack_fffffffffffffe00 >> 0x20);
  local_178 = 0xfffffffffffffffe;
  local_res10 = param_1;
  local_res20 = param_4;
  memset(local_160,0,0x70);
  QueryPerformanceCounter(&local_res8);
  QueryPerformanceFrequency(&local_res18);
  dVar7 = ((double)local_res8.QuadPart / (double)local_res18.QuadPart) * 1000.0;
  if (this[0x25c70] != (CMyAlgorithmManager)0x0) {
    memset(*(void **)(this + 0x25c38),0,(longlong)*(int *)(this + 0x25c2c));
    *(undefined4 *)(this + 0x25c20) = 0;
  }
  QueryPerformanceCounter(&local_res8);
  QueryPerformanceFrequency(&local_res18);
  dVar2 = ((double)local_res8.QuadPart / (double)local_res18.QuadPart) * 1000.0;
  iVar12 = *(int *)(this + 0x25c64);
  iVar11 = *(int *)(this + 0x25c6c);
  if ((*param_3 == *(int *)(this + 0x25c20)) || (*(int *)(this + 0x25c20) == 0)) {
    QueryPerformanceCounter(&local_res8);
    QueryPerformanceFrequency(&local_res18);
    dVar4 = ((double)local_res8.QuadPart / (double)local_res18.QuadPart) * 1000.0;
    iVar16 = param_3[1];
    if (iVar11 + iVar12 <= iVar16) {
      if (param_3[2] == 0x1080001) {
        iVar15 = *param_3;
        local_res8.s.LowPart = iVar15 * iVar12;
        iVar16 = (iVar16 + iVar12) * iVar15;
        iVar15 = iVar15 * iVar11;
      }
      else {
        iVar15 = *param_3;
        local_res8.s.LowPart = iVar15 * iVar12 * 3;
        iVar16 = (iVar16 + iVar12) * iVar15 * 3;
        iVar15 = iVar15 * iVar11 * 3;
      }
      QueryPerformanceCounter(&local_res18);
      QueryPerformanceFrequency(&local_198);
      dVar3 = ((double)local_res18.QuadPart / (double)local_198.QuadPart) * 1000.0;
      pCVar1 = this + 0x25c38;
      if ((*(longlong *)pCVar1 == 0) || (iVar16 != *(int *)(this + 0x25c2c))) {
        QueryPerformanceCounter(&local_res18);
        QueryPerformanceFrequency(&local_198);
        dVar6 = ((double)local_res18.QuadPart / (double)local_198.QuadPart) * 1000.0;
        iVar11 = (**(code **)(*(longlong *)(this + 0x10) + 8))
                           (this + 0x10,*(undefined4 *)(this + 0x24938),(longlong)iVar16,pCVar1,
                            this + 0x25c78,0);
        QueryPerformanceCounter(&local_res18);
        QueryPerformanceFrequency(&local_198);
        dVar5 = ((double)local_res18.QuadPart / (double)local_198.QuadPart) * 1000.0;
        if ((iVar11 != 0) || (*(longlong *)pCVar1 == 0)) {
          *(undefined4 *)(this + 0x25c2c) = 0;
          return -0x1ffffffe;
        }
        dVar18 = FUN_18000c900();
        uVar22 = SUB84(dVar18,0);
        uVar20 = (undefined4)((ulonglong)dVar18 >> 0x20);
        memset(*(void **)pCVar1,0,(longlong)iVar16);
        dVar18 = FUN_18000c900();
        uVar17 = SUB84(dVar18,0);
        uVar21 = (undefined4)((ulonglong)dVar18 >> 0x20);
      }
      else {
        uVar17 = (undefined4)local_130;
        uVar21 = (undefined4)((ulonglong)local_130 >> 0x20);
        uVar22 = (undefined4)local_138;
        uVar20 = (undefined4)((ulonglong)local_138 >> 0x20);
        dVar6 = local_148;
        dVar5 = local_140;
      }
      *(int *)(this + 0x25c2c) = iVar16;
      QueryPerformanceCounter(&local_res18);
      QueryPerformanceFrequency(&local_198);
      dVar18 = ((double)local_res18.QuadPart / (double)local_198.QuadPart) * 1000.0;
      uVar8 = local_res8.s.LowPart;
      local_198 = (LARGE_INTEGER)(longlong)(int)local_res8.s.LowPart;
      memcpy_s((void *)(local_198.QuadPart + *(longlong *)pCVar1),
               (longlong)(iVar16 - local_res8._0_4_),*(void **)(param_3 + 6),(longlong)param_3[3]);
      QueryPerformanceCounter(&local_res8);
      QueryPerformanceFrequency(&local_res18);
      pCVar9 = local_res20;
      local_188 = ((double)local_res8.QuadPart / (double)local_res18.QuadPart) * 1000.0;
      *(int *)(this + 0x25c24) = param_3[1] + iVar12;
      *(int *)(this + 0x25c20) = *param_3;
      *(longlong *)(local_res20 + 0x18) = *(longlong *)pCVar1;
      *(int *)(local_res20 + 0x24) = param_3[9];
      *(int *)(local_res20 + 0xc) = param_3[3] + uVar8;
      *(int *)(local_res20 + 0x20) = param_3[3] + uVar8;
      *(int *)(local_res20 + 0x10) = param_3[4];
      *(undefined4 *)(local_res20 + 4) = *(undefined4 *)(this + 0x25c24);
      *(int *)(local_res20 + 8) = param_3[2];
      *(undefined4 *)local_res20 = *(undefined4 *)(this + 0x25c20);
      *(undefined8 *)(local_res20 + 0x28) = *(undefined8 *)(this + 0x25c78);
      QueryPerformanceCounter(&local_res8);
      QueryPerformanceFrequency(&local_res18);
      local_190 = ((double)local_res8.QuadPart / (double)local_res18.QuadPart) * 1000.0;
      SetImageOutput(this,local_res10,pCVar9);
      QueryPerformanceCounter(&local_res8);
      QueryPerformanceFrequency(&local_res18);
      local_180 = ((double)local_res8.QuadPart / (double)local_res18.QuadPart) * 1000.0;
      iVar12 = (**(code **)(*(longlong *)(this + 0x10) + 8))
                         (this + 0x10,*(undefined4 *)(this + 0x24938),(longlong)iVar16,pCVar1,
                          this + 0x25c78,0);
      QueryPerformanceCounter(&local_res8);
      QueryPerformanceFrequency(&local_res18);
      local_res8 = (LARGE_INTEGER)
                   (((double)local_res8.QuadPart / (double)local_res18.QuadPart) * 1000.0);
      if ((iVar12 == 0) && (*(void **)pCVar1 != (void *)0x0)) {
        memcpy_s(*(void **)pCVar1,local_198.QuadPart,
                 (void *)((longlong)iVar15 + *(longlong *)(param_3 + 6)),local_198.QuadPart);
        dVar19 = FUN_18000c900();
        cVar10 = HSlog_ShouldLog(1);
        if (cVar10 != '\0') {
          puVar13 = FUN_180001320();
          if (0xf < (ulonglong)puVar13[3]) {
            puVar13 = (undefined8 *)*puVar13;
          }
          HSlog_LogPrint(1,
                         "[%s] [ModuId:%d] [%s@%d] [hftest] StitchImageOutput %f,%f,%f,%f,%f,%f,%f,%f,%f,%f,%f,%f,%f,%f"
                         ,puVar13,*(undefined4 *)(this + 0x24938),
                         "CMyAlgorithmManager::StitchImageOutput",0x1613,dVar19 - dVar7,
                         dVar2 - dVar7,dVar4 - dVar2,dVar3 - dVar4,dVar6 - dVar3,dVar5 - dVar6,
                         (double)CONCAT44(uVar20,uVar22) - dVar5,
                         (double)CONCAT44(uVar21,uVar17) - (double)CONCAT44(uVar20,uVar22),
                         dVar18 - (double)CONCAT44(uVar21,uVar17),local_188 - dVar18,
                         local_190 - local_188,local_180 - local_190,(double)local_res8 - local_180,
                         dVar19 - (double)local_res8);
        }
        return 0;
      }
      *(undefined4 *)(this + 0x25c2c) = 0;
      return -0x1ffffffe;
    }
    cVar10 = HSlog_ShouldLog(4);
    if (cVar10 == '\0') {
      return -0x1ffffafe;
    }
    if ((DAT_18003cf00 & 1) == 0) {
      DAT_18003cf00 = DAT_18003cf00 | 1;
      FUN_180001360();
      atexit(FUN_18002c980);
    }
    uVar24 = CONCAT44(uVar17,param_3[1]);
    uVar23 = CONCAT44(uVar22,iVar11 + iVar12);
    uVar22 = 0x15b3;
    pcVar14 = "[%s] [ModuId:%d] [%s@%d] StitchHeight=%d, ImageHeight=%d";
  }
  else {
    cVar10 = HSlog_ShouldLog(4);
    if (cVar10 == '\0') {
      return -0x1ffffafe;
    }
    if ((DAT_18003cf00 & 1) == 0) {
      DAT_18003cf00 = DAT_18003cf00 | 1;
      FUN_180001360();
      atexit(FUN_18002c980);
    }
    uVar24 = CONCAT44(uVar17,*(undefined4 *)(this + 0x25c20));
    uVar23 = CONCAT44(uVar22,*param_3);
    uVar22 = 0x15ac;
    pcVar14 = "[%s] [ModuId:%d] [%s@%d] ImageWidth=%d, PreviousImageWidth=%d";
  }
  puVar13 = &DAT_18003cee0;
  if (0xf < DAT_18003cef8) {
    puVar13 = DAT_18003cee0;
  }
  HSlog_LogPrint(4,pcVar14,puVar13,*(undefined4 *)(this + 0x24938),
                 "CMyAlgorithmManager::StitchImageOutput",uVar22,uVar23,uVar24);
  return -0x1ffffafe;
}

