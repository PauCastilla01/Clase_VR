/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * Licensed under the Oculus SDK License Agreement (the "License");
 * you may not use the Oculus SDK except in compliance with the License,
 * which is provided at the time of installation or download, or which
 * otherwise accompanies this software in either electronic or hard copy form.
 *
 * You may obtain a copy of the License at
 *
 * https://developer.oculus.com/licenses/oculussdk/
 *
 * Unless required by applicable law or agreed to in writing, the Oculus SDK
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using Meta.XR.Editor.UserInterface;
using UnityEngine;

namespace Meta.XR.Editor.UserInterface
{
    internal class Button : IUserInterfaceItem
    {
        public bool Hide { get; set; }

        private GUIContent Label { get; }
        private readonly ActionLinkDescription _action;
        private readonly GUILayoutOption[] _options;

        public Button(ActionLinkDescription action, params GUILayoutOption[] options)
        {
            _action = action;
            _options = options;
        }

        public void Draw()
        {
            _action.Style = GUI.skin.button;
            _action.Draw(_options);
        }
    }
}
