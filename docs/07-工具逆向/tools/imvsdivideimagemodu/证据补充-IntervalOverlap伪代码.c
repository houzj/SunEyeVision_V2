
/* private: int __cdecl CIMVSDivideImageModu::IntervalOverlap(float,float,float,float) __ptr64 */

int __thiscall
CIMVSDivideImageModu::IntervalOverlap
          (CIMVSDivideImageModu *this,float param_1,float param_2,float param_3,float param_4)

{
  float fVar1;
  
  fVar1 = param_3;
                    /* 0x1ec10  9  ?IntervalOverlap@CIMVSDivideImageModu@@AEAAHMMMM@Z */
  if (param_4 < param_3) {
    fVar1 = param_4;
    param_4 = param_3;
  }
  if ((fVar1 <= param_2) && (param_1 <= param_4)) {
    return 1;
  }
  return 0;
}

