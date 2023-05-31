/*
 * Copyright 2020, Gregg Tavares.
 * All rights reserved.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are
 * met:
 *
 *     * Redistributions of source code must retain the above copyright
 * notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above
 * copyright notice, this list of conditions and the following disclaimer
 * in the documentation and/or other materials provided with the
 * distribution.
 *     * Neither the name of Gregg Tavares. nor the names of its
 * contributors may be used to endorse or promote products derived from
 * this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
 * "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
 * LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
 * A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT
 * OWNER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
 * SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
 * LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
 * DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
 * THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
 * OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */
#define WEBGL_COPY_AND_PASTE_SUPPORT_TEXTMESH_PRO

using UnityEngine;
using UnityEngine.EventSystems;
using System.Runtime.InteropServices;

public class WebGLCopyAndPasteAPI
{

#if UNITY_WEBGL

    [DllImport("__Internal")]
    private static extern void initWebGLCopyAndPaste(StringCallback cutCopyCallback, StringCallback pasteCallback);
    [DllImport("__Internal")]
    private static extern void passCopyToBrowser(string str);

    delegate void StringCallback( string content );


    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
    private static void Init()
    {
        if ( !Application.isEditor )
        {
            initWebGLCopyAndPaste(GetClipboard, ReceivePaste );
        }
    }

    private static void SendKey(string baseKey)
      {
        string appleKey = "%" + baseKey;
        string naturalKey = "^" + baseKey;

        var currentObj = EventSystem.current.currentSelectedGameObject;
        if (currentObj == null) {
          return;
        }
        {
          var input = currentObj.GetComponent<UnityEngine.UI.InputField>();
          if (input != null) {
            // I don't know what's going on here. The code in InputField
            // is looking for ctrl-c but that fails on Mac Chrome/Firefox
            input.ProcessEvent(Event.KeyboardEvent(naturalKey));
            input.ProcessEvent(Event.KeyboardEvent(appleKey));
            // so let's hope one of these is basically a noop
            return;
          }
        }
#if WEBGL_COPY_AND_PASTE_SUPPORT_TEXTMESH_PRO
        {
          var input = currentObj.GetComponent<TMPro.TMP_InputField>();
          if (input != null) {
            // I don't know what's going on here. The code in InputField
            // is looking for ctrl-c but that fails on Mac Chrome/Firefox
            // so let's hope one of these is basically a noop
            input.ProcessEvent(Event.KeyboardEvent(naturalKey));
            input.ProcessEvent(Event.KeyboardEvent(appleKey));
            return;
          }
        }
#endif
      }

      [AOT.MonoPInvokeCallback( typeof(StringCallback) )]
      private static void GetClipboard(string key)
      {
        SendKey(key);
        passCopyToBrowser(GUIUtility.systemCopyBuffer);
      }

      [AOT.MonoPInvokeCallback( typeof(StringCallback) )]
      private static void ReceivePaste(string str)
      {
        GUIUtility.systemCopyBuffer = str;
      }

#endif

}
