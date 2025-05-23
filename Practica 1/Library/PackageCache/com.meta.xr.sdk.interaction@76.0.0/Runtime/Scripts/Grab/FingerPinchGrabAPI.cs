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

using Oculus.Interaction.Input;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Assertions;

namespace Oculus.Interaction.GrabAPI
{
    [StructLayout(LayoutKind.Sequential)]
    public class HandPinchData
    {
        private const int NumHandJoints = 24;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = NumHandJoints * 3, ArraySubType = UnmanagedType.R4)]
        private readonly float[] _jointPositions;

        public HandPinchData()
        {
            int floatCount = NumHandJoints * 3;
            _jointPositions = new float[floatCount];
        }

        public void SetJoints(IReadOnlyList<Pose> poses)
        {
            Assert.AreEqual(NumHandJoints, poses.Count);
            int floatIndex = 0;
            for (int jointIndex = 0; jointIndex < NumHandJoints; jointIndex++)
            {
                Vector3 position = poses[jointIndex].position;
                _jointPositions[floatIndex++] = position.x;
                _jointPositions[floatIndex++] = position.y;
                _jointPositions[floatIndex++] = position.z;
            }
        }

        public void SetJoints(IReadOnlyList<Vector3> positions)
        {
            Assert.AreEqual(NumHandJoints, positions.Count);
            int floatIndex = 0;
            for (int jointIndex = 0; jointIndex < NumHandJoints; jointIndex++)
            {
                Vector3 position = positions[jointIndex];
                _jointPositions[floatIndex++] = position.x;
                _jointPositions[floatIndex++] = position.y;
                _jointPositions[floatIndex++] = position.z;
            }
        }
    }

    /// <summary>
    /// This <see cref="IFingerAPI"/> uses an advanced calculation for the pinch value of the fingers
    /// to detect if they are grabbing.
    /// </summary>
    /// <remarks>
    /// The implementation details of this pinch calculation are more low-level than Unity and are thus encapsulated
    /// below the managed-native boundary. This type merely provides an API surface through which to invoke the
    /// native functionality.
    /// </remarks>
    public class FingerPinchGrabAPI : IFingerAPI
    {
        enum ReturnValue { Success = 0, Failure = -1 };

        #region DLLImports

        [DllImport("InteractionSdk")]
        private static extern int isdk_FingerPinchGrabAPI_Create();

        [DllImport("InteractionSdk")]
        private static extern ReturnValue isdk_FingerPinchGrabAPI_UpdateHandData(int handle, [In] HandPinchData data);

        [DllImport("InteractionSdk")]
        private static extern ReturnValue isdk_FingerPinchGrabAPI_UpdateHandWristHMDData(int handle, [In] HandPinchData data, in Vector3 wristForward, in Vector3 hmdForward);

        [DllImport("InteractionSdk", CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
        private static extern bool isdk_FingerPinchGrabAPI_GetString(int handle, [MarshalAs(UnmanagedType.LPStr)] string name, out IntPtr val);

        [DllImport("InteractionSdk")]
        private static extern ReturnValue isdk_FingerPinchGrabAPI_GetFingerIsGrabbing(int handle, int index, out bool grabbing);

        [DllImport("InteractionSdk")]
        private static extern ReturnValue isdk_FingerPinchGrabAPI_GetFingerPinchPercent(int handle, int index, out float pinchPercent);

        [DllImport("InteractionSdk")]
        private static extern ReturnValue isdk_FingerPinchGrabAPI_GetFingerPinchDistance(int handle, int index, out float pinchDistance);

        [DllImport("InteractionSdk")]
        private static extern ReturnValue isdk_FingerPinchGrabAPI_GetFingerIsGrabbingChanged(int handle, int index, bool targetState, out bool grabbing);

        [DllImport("InteractionSdk")]
        private static extern ReturnValue isdk_FingerPinchGrabAPI_GetFingerGrabScore(int handle, HandFinger finger, out float outScore);

        [DllImport("InteractionSdk")]
        private static extern ReturnValue isdk_FingerPinchGrabAPI_GetCenterOffset(int handle, out Vector3 outCenter);

        [DllImport("InteractionSdk")]
        private static extern int isdk_Common_GetVersion(out IntPtr versionStringPtr);

        [DllImport("InteractionSdk")]
        private static extern ReturnValue isdk_FingerPinchGrabAPI_GetPinchGrabParam(int handle, PinchGrabParam paramId, out float outParam);

        [DllImport("InteractionSdk")]
        private static extern ReturnValue isdk_FingerPinchGrabAPI_SetPinchGrabParam(int handle, PinchGrabParam paramId, float param);

        [DllImport("InteractionSdk")]
        private static extern ReturnValue isdk_FingerPinchGrabAPI_IsPinchVisibilityGood(int handle, out bool outVal);
        #endregion

        private int _fingerPinchGrabApiHandle = -1;
        private HandPinchData _pinchData = new HandPinchData();

        private IHmd _hmd = null;

        public FingerPinchGrabAPI(IHmd hmd = null)
        {
            _hmd = hmd;
        }

        private int GetHandle()
        {
            if (_fingerPinchGrabApiHandle == -1)
            {
                _fingerPinchGrabApiHandle = isdk_FingerPinchGrabAPI_Create();
                Debug.Assert(_fingerPinchGrabApiHandle != -1, "FingerPinchGrabAPI: isdk_FingerPinchGrabAPI_Create failed");
            }

            return _fingerPinchGrabApiHandle;
        }

        /// <summary>
        /// Sets the current value of <paramref name="paramId"/>.
        /// </summary>
        /// <param name="paramId">The <see cref="PinchGrabParam"/> for which to set the value</param>
        /// <param name="paramVal">The new value <paramref name="paramId"/> should be set to</param>
        public void SetPinchGrabParam(PinchGrabParam paramId, float paramVal)
        {
            ReturnValue rc = isdk_FingerPinchGrabAPI_SetPinchGrabParam(GetHandle(), paramId, paramVal);
            Debug.Assert(rc != ReturnValue.Failure, "FingerPinchGrabAPI: isdk_FingerPinchGrabAPI_SetPinchGrabParam failed");
        }

        /// <summary>
        /// Retrieves the current value of <paramref name="paramId"/>.
        /// </summary>
        /// <param name="paramId">The <see cref="PinchGrabParam"/> for which to retrieve the value</param>
        /// <returns>The current value of <paramref name="paramId"/></returns>
        public float GetPinchGrabParam(PinchGrabParam paramId)
        {
            ReturnValue rc = isdk_FingerPinchGrabAPI_GetPinchGrabParam(GetHandle(), paramId, out float paramVal);
            Debug.Assert(rc != ReturnValue.Failure, "FingerPinchGrabAPI: isdk_FingerPinchGrabAPI_GetPinchGrabParam failed");
            return paramVal;
        }

        /// <summary>
        /// Checks whether hand tracking has good visibility on a pinch.
        /// </summary>
        /// <returns>True if the hand tracking system believes it can see pinching well, false otherwise</returns>
        public bool GetIsPinchVisibilityGood()
        {
            bool b;
            ReturnValue rc = isdk_FingerPinchGrabAPI_IsPinchVisibilityGood(GetHandle(), out b);
            Debug.Assert(rc != ReturnValue.Failure, "FingerPinchGrabAPI: isdk_FingerPinchGrabAPI_GetIsPinchVisibilityGood failed");
            return b;
        }

        /// <summary>
        /// Implementation of <see cref="IFingerAPI.GetFingerIsGrabbing(HandFinger)"/>; for details, please refer to
        /// the related documentation provided for that property.
        /// </summary>
        public bool GetFingerIsGrabbing(HandFinger finger)
        {
            ReturnValue rc = isdk_FingerPinchGrabAPI_GetFingerIsGrabbing(GetHandle(), (int)finger, out bool grabbing);
            Debug.Assert(rc != ReturnValue.Failure, "FingerPinchGrabAPI: isdk_FingerPinchGrabAPI_GetFingerIsGrabbing failed");
            return grabbing;
        }

        /// <summary>
        /// Determines the degree to which a particular finger is pinching.
        /// </summary>
        /// <param name="finger">The <see cref="HandFinger"/> to check</param>
        /// <returns>A percentage representation of how pinched <paramref name="finger"/> is</returns>
        public float GetFingerPinchPercent(HandFinger finger)
        {
            ReturnValue rc = isdk_FingerPinchGrabAPI_GetFingerPinchPercent(GetHandle(), (int)finger, out float pinchPercent);
            Debug.Assert(rc != ReturnValue.Failure, "FingerPinchGrabAPI: isdk_FingerPinchGrabAPI_GetFingerPinchPercent failed");
            return pinchPercent;
        }

        /// <summary>
        /// Gets the finger pinch distance for the requested finger.
        /// </summary>
        /// <param name="finger">The <see cref="HandFinger"/> for which to check the pinch distance</param>
        /// <returns></returns>
        public float GetFingerPinchDistance(HandFinger finger)
        {
            ReturnValue rc = isdk_FingerPinchGrabAPI_GetFingerPinchDistance(GetHandle(), (int)finger, out float pinchDistance);
            Debug.Assert(rc != ReturnValue.Failure, "FingerPinchGrabAPI: isdk_FingerPinchGrabAPI_GetFingerPinchDistance failed");
            return pinchDistance;
        }

        /// <summary>
        /// Gets the offset between the wrist and the pinch point.
        /// </summary>
        /// <returns>The offset between the wrist and the pinch point</returns>
        public Vector3 GetWristOffsetLocal()
        {
            ReturnValue rc = isdk_FingerPinchGrabAPI_GetCenterOffset(GetHandle(), out Vector3 center);
            Debug.Assert(rc != ReturnValue.Failure, "FingerPinchGrabAPI: isdk_FingerPinchGrabAPI_GetCenterOffset failed");
            return center;
        }

        /// <summary>
        /// Implementation of <see cref="IFingerAPI.GetFingerIsGrabbingChanged(HandFinger, bool)"/>; for details, please refer to
        /// the related documentation provided for that property.
        /// </summary>
        public bool GetFingerIsGrabbingChanged(HandFinger finger, bool targetPinchState)
        {
            ReturnValue rc = isdk_FingerPinchGrabAPI_GetFingerIsGrabbingChanged(GetHandle(), (int)finger, targetPinchState, out bool changed);
            Debug.Assert(rc != ReturnValue.Failure, "FingerPinchGrabAPI: isdk_FingerPinchGrabAPI_GetFingerIsGrabbingChanged failed");
            return changed;
        }

        /// <summary>
        /// Implementation of <see cref="IFingerAPI.GetFingerGrabScore(HandFinger)"/>; for details, please refer to
        /// the related documentation provided for that property.
        /// </summary>
        public float GetFingerGrabScore(HandFinger finger)
        {
            ReturnValue rc = isdk_FingerPinchGrabAPI_GetFingerGrabScore(GetHandle(), finger, out float score);
            Debug.Assert(rc != ReturnValue.Failure, "FingerPinchGrabAPI: isdk_FingerPinchGrabAPI_GetFingerGrabScore failed");
            return score;
        }

        /// <summary>
        /// Implementation of <see cref="IFingerAPI.Update(IHand)"/>; for details, please refer to
        /// the related documentation provided for that property.
        /// </summary>
        public void Update(IHand hand)
        {
            hand.GetJointPosesFromWrist(out var poses);
            hand.GetJointPose(HandJointId.HandWristRoot, out Pose wristPose);
            Update(poses, hand.Handedness, wristPose);
        }

        internal void Update(IReadOnlyList<Pose> handPoses, Handedness handedness, Pose wristPose)
        {
            if (handPoses.Count <= 0)
            {
                return;
            }
            _pinchData.SetJoints(handPoses);
            Vector3 wristForward = Vector3.forward;
            Vector3 hmdForward = Vector3.forward;

            if (_hmd != null &&
                _hmd.TryGetRootPose(out Pose centerEyePose))
            {
                wristForward = -1.0f * wristPose.forward;
                hmdForward = -1.0f * centerEyePose.forward;
                if (handedness == Handedness.Right)
                {
                    wristForward = -wristForward;
                }
            }

            ReturnValue rc = isdk_FingerPinchGrabAPI_UpdateHandWristHMDData(GetHandle(), _pinchData, wristForward, hmdForward);
            Debug.Assert(rc != ReturnValue.Failure, "FingerPinchGrabAPI: isdk_FingerPinchGrabAPI_UpdateHandWristHMDData failed");
        }

    }
}
