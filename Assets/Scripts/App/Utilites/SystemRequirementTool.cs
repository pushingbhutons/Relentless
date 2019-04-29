﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using Loom.ZombieBattleground.Common;

namespace Loom.ZombieBattleground
{
    public static class SystemRequirementTool
    {
        public static bool CheckInternetConnectionReachability()
        {
            return Application.internetReachability != NetworkReachability.NotReachable;
        }

        public static bool CheckIfMeetMinimumSystemRequirement()
        {  
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
            //Many devices won't report the memory size exactly, so we lower memory size threshold a bit
            return SystemInfo.systemMemorySize >= Mathf.CeilToInt
            (
                Constants.MinimumMemorySize * Constants.MinimumMemoryThresholdPercentage
            );
#else
            return true;
#endif
        }
    }
}