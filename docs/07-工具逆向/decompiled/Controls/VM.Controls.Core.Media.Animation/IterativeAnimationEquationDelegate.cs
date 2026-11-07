using System;

namespace VM.Controls.Core.Media.Animation;

public delegate T IterativeAnimationEquationDelegate<T>(TimeSpan currentTime, T from, T to, TimeSpan duration);
