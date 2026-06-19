using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using VRBadminton.Input;

namespace VRBadminton.Tests
{
    public sealed class BadmintonInputTests
    {
        [Test]
        public void RacketFrameJsonParsesMatrixAndQuaternion()
        {
            const string json = "{" +
                "\"type\":\"racket_frame\"," +
                "\"timestamp\":1," +
                "\"clientId\":\"phone-test\"," +
                "\"aligned\":true," +
                "\"sessionId\":\"s1\"," +
                "\"orientation\":[0,0,0,1]," +
                "\"rotationMatrix\":[1,0,0,0,1,0,0,0,1]," +
                "\"angularVelocity\":[0,500,0]," +
                "\"acceleration\":[0,0,9.8]," +
                "\"angularSpeed\":500," +
                "\"raw\":{\"alpha\":10,\"beta\":20,\"gamma\":30}" +
                "}";

            bool ok = PhoneRacketHttpServer.TryParseRacketFrameJson(json, 1234, out BadmintonRacketFrame frame);

            Assert.IsTrue(ok);
            Assert.AreEqual("phone-test", frame.ClientId);
            Assert.IsTrue(frame.Aligned);
            Assert.AreEqual(1234, frame.Timestamp);
            Assert.AreEqual(500f, frame.AngularVelocity.y, 0.001f);
            Assert.AreEqual(1f, frame.RotationMatrix.m00, 0.001f);
            Assert.AreEqual(1f, frame.Orientation.w, 0.001f);
            Assert.AreEqual(20f, frame.RawEuler.x, 0.001f);
        }

        [Test]
        public void SwingDetectorTriggersFastSwingAndUsesCooldown()
        {
            BadmintonSwingDetector detector = new BadmintonSwingDetector();
            BadmintonRacketFrame slow = BadmintonRacketFrame.Default();
            slow.AngularVelocity = new Vector3(0f, 180f, 0f);
            slow.AngularSpeed = 180f;
            detector.Update(slow, 1000);

            BadmintonRacketFrame fast = BadmintonRacketFrame.Default();
            fast.AngularVelocity = new Vector3(0f, 720f, 0f);
            fast.AngularSpeed = 720f;
            BadmintonSwingSample impact = detector.Update(fast, 1080);
            BadmintonSwingSample cooldown = detector.Update(fast, 1160);

            Assert.AreEqual(BadmintonSwingState.ImpactCandidate, impact.State);
            Assert.IsTrue(impact.Impact);
            Assert.AreEqual(BadmintonSwingType.Underhand, impact.Type);
            Assert.IsFalse(cooldown.Impact);
        }

        [Test]
        public void PoseMapperTracksPlayerPositionAndRightHand()
        {
            BadmintonPoseLandmarkMapper mapper = new BadmintonPoseLandmarkMapper();
            List<BadmintonPoseLandmark> neutral = Pose(0.5f, 1f, 0.35f);
            BadmintonPlayerFrame frame = BadmintonPlayerFrame.Default();
            for (int i = 0; i < 9; i++)
            {
                frame = mapper.BuildFrame(neutral, 1000 + i * 33, "camera-test");
            }

            Assert.IsTrue(frame.Visible);
            Assert.IsTrue(frame.Calibrated);
            Assert.IsTrue(frame.RightHand.Visible);
            Assert.Greater(frame.RightHand.Height, 0.5f);

            BadmintonPlayerFrame shifted = mapper.BuildFrame(Pose(0.44f, 1.18f, 0.24f), 1400, "camera-test");

            Assert.AreNotEqual(0f, shifted.VirtualPosition.x);
            Assert.Greater(shifted.VirtualPosition.z, 0f);
            Assert.AreEqual("right_hand", shifted.RightHand.Source);
        }

        [Test]
        public void MatrixQuaternionConversionHandlesIdentity()
        {
            float[] matrix = {1f, 0f, 0f, 0f, 1f, 0f, 0f, 0f, 1f};

            Matrix4x4 parsed = BadmintonInputMath.MatrixFromRowMajor(matrix);
            Quaternion quaternion = BadmintonInputMath.QuaternionFromRowMajor(matrix);

            Assert.AreEqual(1f, parsed.m00, 0.001f);
            Assert.AreEqual(1f, parsed.m11, 0.001f);
            Assert.AreEqual(1f, parsed.m22, 0.001f);
            Assert.AreEqual(0f, quaternion.x, 0.001f);
            Assert.AreEqual(0f, quaternion.y, 0.001f);
            Assert.AreEqual(0f, quaternion.z, 0.001f);
            Assert.AreEqual(1f, quaternion.w, 0.001f);
        }

        [Test]
        public void MirrorForwardBackReversesPitchForUnityFacing()
        {
            const float degrees = 30f;
            float c = Mathf.Cos(degrees * Mathf.Deg2Rad);
            float s = Mathf.Sin(degrees * Mathf.Deg2Rad);
            float[] pitchForward =
            {
                1f, 0f, 0f,
                0f, c, -s,
                0f, s, c
            };

            Matrix4x4 original = BadmintonInputMath.MatrixFromRowMajor(pitchForward);
            Matrix4x4 mirrored = BadmintonInputMath.MirrorForwardBack(original);
            Vector3 originalForward = BadmintonInputMath.TransformDirection(original, Vector3.forward);
            Vector3 mirroredForward = BadmintonInputMath.TransformDirection(mirrored, Vector3.forward);

            Assert.AreEqual(-originalForward.y, mirroredForward.y, 0.001f);
            Assert.AreEqual(originalForward.z, mirroredForward.z, 0.001f);
        }

        [Test]
        public void RacketFrameJsonPrefersRotationMatrixOverQuaternionPayload()
        {
            const string json = "{" +
                "\"type\":\"racket_frame\"," +
                "\"timestamp\":1," +
                "\"clientId\":\"phone-test\"," +
                "\"aligned\":true," +
                "\"orientation\":[0,0,0,1]," +
                "\"rotationMatrix\":[-1,0,0,0,1,0,0,0,-1]," +
                "\"angularVelocity\":[0,0,0]," +
                "\"angularSpeed\":0," +
                "\"raw\":{\"alpha\":0,\"beta\":0,\"gamma\":0}" +
                "}";

            bool ok = PhoneRacketHttpServer.TryParseRacketFrameJson(json, 1234, out BadmintonRacketFrame frame);

            Assert.IsTrue(ok);
            Assert.AreEqual(0f, frame.Orientation.x, 0.001f);
            Assert.AreEqual(1f, Mathf.Abs(frame.Orientation.y), 0.001f);
            Assert.AreEqual(0f, frame.Orientation.z, 0.001f);
            Assert.AreEqual(0f, frame.Orientation.w, 0.001f);
        }

        private static List<BadmintonPoseLandmark> Pose(float centerX, float scale, float handY)
        {
            List<BadmintonPoseLandmark> landmarks = new List<BadmintonPoseLandmark>();
            for (int i = 0; i < 33; i++)
            {
                landmarks.Add(new BadmintonPoseLandmark(0f, 0f, 0f));
            }

            float shoulderWidth = 0.24f * scale;
            float hipWidth = 0.17f * scale;
            float shoulderY = 0.48f;
            float hipY = shoulderY + 0.25f * scale;
            landmarks[BadmintonPoseLandmarkMapper.Nose] = new BadmintonPoseLandmark(centerX, shoulderY - 0.17f, 1f);
            landmarks[BadmintonPoseLandmarkMapper.LeftShoulder] = new BadmintonPoseLandmark(centerX + shoulderWidth * 0.5f, shoulderY, 1f);
            landmarks[BadmintonPoseLandmarkMapper.RightShoulder] = new BadmintonPoseLandmark(centerX - shoulderWidth * 0.5f, shoulderY, 1f);
            landmarks[BadmintonPoseLandmarkMapper.LeftHip] = new BadmintonPoseLandmark(centerX + hipWidth * 0.5f, hipY, 1f);
            landmarks[BadmintonPoseLandmarkMapper.RightHip] = new BadmintonPoseLandmark(centerX - hipWidth * 0.5f, hipY, 1f);
            landmarks[BadmintonPoseLandmarkMapper.RightElbow] = new BadmintonPoseLandmark(centerX - shoulderWidth * 0.8f, handY + 0.08f, 0.9f);
            landmarks[BadmintonPoseLandmarkMapper.RightWrist] = new BadmintonPoseLandmark(centerX - shoulderWidth * 1.05f, handY + 0.03f, 0.9f);
            landmarks[BadmintonPoseLandmarkMapper.RightIndex] = new BadmintonPoseLandmark(centerX - shoulderWidth * 1.18f, handY, 0.9f);
            landmarks[BadmintonPoseLandmarkMapper.RightPinky] = new BadmintonPoseLandmark(centerX - shoulderWidth * 1.12f, handY + 0.03f, 0.9f);
            landmarks[BadmintonPoseLandmarkMapper.RightThumb] = new BadmintonPoseLandmark(centerX - shoulderWidth * 1.22f, handY + 0.015f, 0.9f);
            return landmarks;
        }
    }
}
