﻿// Copyright (c) 2018 - Loom Network. All rights reserved.
// https://loomx.io/


using LoomNetwork.CZB.Common;
using UnityEngine;
using UnityEngine.UI;

namespace LoomNetwork.CZB
{
    [RequireComponent(typeof(Text))]
    public class SetAppVersionText : MonoBehaviour
    {
        private void Start()
        {
            GetComponent<Text>().text = Constants.VERSION_REVISION + Constants.SPACE + Constants.CURRENT_VERSION;
        }
    }
}