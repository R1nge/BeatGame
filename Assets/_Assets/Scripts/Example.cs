﻿/*
 * Copyright (c) 2015 Allan Pichardo
 * 
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 * 
 *  http://www.apache.org/licenses/LICENSE-2.0
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using UnityEngine;

public class Example : MonoBehaviour
{
    private void Awake()
    {
        AudioProcessor processor = FindObjectOfType<AudioProcessor>();
        processor.onSpectrum.AddListener(SpectrumChanged);
    }

    private void SpectrumChanged(float[] spectrum)
    {
        //The spectrum is logarithmically averaged to 12 bands

        for (int i = 0; i < spectrum.Length; ++i)
        {
            var start = new Vector3(i, 0, 0);
            var end = new Vector3(i, spectrum[i], 0);
            Debug.DrawLine(start, end);
        }
    }
}