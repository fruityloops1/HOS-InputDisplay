using Raylib_cs;
using System.Net;
using System.Net.Sockets;
using System.Numerics;
using System;
using System.Diagnostics;

namespace InputDisplay;

class Program
{
    private static Config config = new Config("Res/config.ini");
    private static Font buttonFont, fatFont;
    private static Camera3D camera;
    private static Model proControllerModel, joyLeftModel, joyRightModel;

    public static UInt64 Keys;
    public static Vector2 LeftStick, RightStick;
    public static Color buttonColor, controllerColorLeft, controllerColorRight;

    private static void DrawButton(Vector2 pos, float width, float height, HidNpadButton button, string label, float roundness, float fontOffsetX, float fontOffsetY, float fontSize)
    {
        Rectangle rec = new Rectangle(pos.X - width, pos.Y - height, width * 2, height * 2);
        Raylib.DrawRectangleRounded(rec, roundness, 8, (Keys & Convert.ToUInt64(button)) != 0 ? config.ActiveColor : config.InactiveColor);
        pos.X += fontOffsetX;
        pos.Y += fontOffsetY;
        Raylib.DrawTextEx(buttonFont, label, pos, fontSize, 4, config.UseSystemButtonColor ? GetTextColor(buttonColor) : config.FontColor);
    }

    private static void SetButtonColor(HidNpadButton button, int materialIndex, int materialIndexJoyL, int materialIndexJoyR)
    {
        unsafe
        {
            if (materialIndex != -1)
                proControllerModel.materials[materialIndex].maps[0].color = (Keys & Convert.ToUInt64(button)) != 0 ? config.ActiveColor : config.InactiveColor;
            if (materialIndexJoyL != -1)
                joyLeftModel.materials[materialIndexJoyL].maps[0].color = (Keys & Convert.ToUInt64(button)) != 0 ? config.ActiveColor : config.InactiveColor;
            if (materialIndexJoyR != -1)
                joyRightModel.materials[materialIndexJoyR].maps[0].color = (Keys & Convert.ToUInt64(button)) != 0 ? config.ActiveColor : config.InactiveColor;
        }
    }

    private static void DrawStick(Vector2 pos, Vector2 stick, HidNpadButton button)
    {
        Vector2 at = new Vector2(pos.X + stick.X / 800, pos.Y + -stick.Y / 800);
        Raylib.DrawRing(pos, 56, 50, 0, 360, 40, config.InactiveColor);
        Raylib.DrawCircleV(at, 40, (Keys & Convert.ToUInt64(button)) != 0 ? config.ActiveColor : config.StickColor);
    }

    private static void SetStickColor(HidNpadButton button, int materialIndex, int materialIndexJoyL, int materialIndexJoyR)
    {
        unsafe
        {
            if (materialIndex != -1)
                proControllerModel.materials[materialIndex].maps[0].color = (Keys & Convert.ToUInt64(button)) != 0 ? config.ActiveColor : config.StickColor;
            if (materialIndexJoyL != -1)
                joyLeftModel.materials[materialIndexJoyL].maps[0].color = (Keys & Convert.ToUInt64(button)) != 0 ? config.ActiveColor : config.UseSystemButtonColor ? config.InactiveColor : config.StickColor;
            if (materialIndexJoyR != -1)
                joyRightModel.materials[materialIndexJoyR].maps[0].color = (Keys & Convert.ToUInt64(button)) != 0 ? config.ActiveColor : config.UseSystemButtonColor ? config.InactiveColor : config.StickColor;
        }
    }

    private static float[,] GetDirectionMatrix(byte[] data, int index, int controllerIndex)
    {
        int start = 24 + 96 * index + 52;
        float[,] directionMatrix = {
                {BitConverter.ToSingle(data, GetOffset(start, controllerIndex)), BitConverter.ToSingle(data, GetOffset(start+4, controllerIndex)), BitConverter.ToSingle(data, GetOffset(start+8, controllerIndex))},
                {BitConverter.ToSingle(data, GetOffset(start+12, controllerIndex)), BitConverter.ToSingle(data, GetOffset(start+16, controllerIndex)), BitConverter.ToSingle(data, GetOffset(start+20, controllerIndex))},
                {BitConverter.ToSingle(data, GetOffset(start+24, controllerIndex)), BitConverter.ToSingle(data, GetOffset(start+28, controllerIndex)), BitConverter.ToSingle(data, GetOffset(start+32, controllerIndex))}
            };
        return directionMatrix;
    }

    private static Quaternion GetControllerQuat(byte[] data, int index, int controllerIndex)
    {
        float[,] directionMatrix = GetDirectionMatrix(data, index, controllerIndex);
        Quaternion quat = MathUtil.ToQuaternion(directionMatrix);
        quat.X *= -1;
        quat.Y *= -1;
        quat.Z *= -1;
        return quat;
    }

    private static Color GetTextColor(Color backgroundColor)
    {
        float luminosity = (backgroundColor.r * 0.299f + backgroundColor.g * 0.587f + backgroundColor.b * 0.114f) / 255f;
        return luminosity > 0.5f ? Color.BLACK : Color.WHITE;
    }

    public static Color GetStickColor(Color buttonColor)
    {
        float luminosity = (buttonColor.r * 0.299f + buttonColor.g * 0.587f + buttonColor.b * 0.114f) / 255f;
        int adjustFactor = luminosity > 0.5f ? -65 : 65;

        buttonColor.r = (byte)Math.Clamp(buttonColor.r + adjustFactor, 0, 255);
        buttonColor.g = (byte)Math.Clamp(buttonColor.g + adjustFactor, 0, 255);
        buttonColor.b = (byte)Math.Clamp(buttonColor.b + adjustFactor, 0, 255);
        return buttonColor;
    }

    private static int GetOffset(int structOffset, int controllerIdx)
    {
        return 8 + 240 * controllerIdx + structOffset;
    }

    public static void Main()
    {
        Raylib.InitWindow(619, 360, "HOS-InputDisplay");
        buttonFont = Raylib.LoadFontEx(config.FontPath, 38, null, 0);
        fatFont = Raylib.LoadFontEx("Res/Montserrat-Black.ttf", 38, null, 0);
        camera.position = new Vector3(0, -15, 15);
        camera.target = new Vector3(0, 0, -6);
        camera.up = new Vector3(0, 1, 0);
        camera.fovy = 45;
        camera.projection = CameraProjection.CAMERA_PERSPECTIVE;
        proControllerModel = Raylib.LoadModel("Res/Controller.glb");
        joyLeftModel = Raylib.LoadModel("Res/JoyL.glb");
        joyRightModel = Raylib.LoadModel("Res/JoyR.glb");
        Stopwatch stopwatch = new Stopwatch();
        stopwatch.Start();

        RenderTexture2D[] motionTextures = new RenderTexture2D[8];
        for (int i = 0; i < 8; i++)
            motionTextures[i] = Raylib.LoadRenderTexture(619, 360);

        UdpClient client = new UdpClient(config.Host, 44302);
        IPEndPoint remote = null;

        byte[] sendData = new byte[] { 0, 0, 0, 0 };
        client.Send(sendData, sendData.Length);

        if (config.PacketOnVsyncEvent)
            sendData = new byte[] { 1, 0, 0, 0, 255, 255, 255, 255 };
        else
            sendData = new byte[] { 1, 0, 0, 0, (byte)config.PacketsPerSecond, 0, 0, 0 };
        client.Send(sendData, sendData.Length);

        Quaternion unit = new Quaternion(0, 0, 0, 1);
        Quaternion[] slerpTo = new Quaternion[8] { unit, unit, unit, unit, unit, unit, unit, unit };
        Quaternion[] baseRotation = new Quaternion[8] { unit, unit, unit, unit, unit, unit, unit, unit };
        Quaternion[] slerpTo2 = new Quaternion[8] { unit, unit, unit, unit, unit, unit, unit, unit };
        Quaternion[] baseRotation2 = new Quaternion[8] { unit, unit, unit, unit, unit, unit, unit, unit };
        int[] calibrationCooldown = new int[8] { -1, -1, -1, -1, -1, -1, -1, -1 };


        unsafe
        {
            proControllerModel.materials[2].maps[0].color = config.ControllerColor;
            proControllerModel.materials[7].maps[0].color = config.InactiveColor;
        }

        long packetsReceived = 0;
        while (true)
        {
            packetsReceived++;

            float fps = 1000f / ((float)stopwatch.ElapsedMilliseconds / (float)packetsReceived);
            if (config.PacketOnVsyncEvent)
                config.PacketsPerSecond = (int)fps;

            byte[] data = client.Receive(ref remote);
            uint numControllers = BitConverter.ToUInt32(data, 0);
            if (numControllers == 0)
                Raylib.SetWindowSize(1, 360);
            else
                Raylib.SetWindowSize((int)(619 * numControllers), 360);



            for (int i = 0; i < numControllers; i++)
            {
                int styleSet = BitConverter.ToInt32(data, GetOffset(232, i));
                int colorButton = BitConverter.ToInt32(data, GetOffset(220, i));
                int leftColor = BitConverter.ToInt32(data, GetOffset(216, i));
                int rightColor = BitConverter.ToInt32(data, GetOffset(224, i));
                controllerColorLeft = new Color((byte)(leftColor & 0xff),
                                  (byte)((leftColor & 0xff00) >> 8),
                                  (byte)((leftColor & 0xff0000) >> 0x10),
                                   (byte)((leftColor & -16777216) >> 0x18)
                                  );
                controllerColorRight = new Color((byte)(rightColor & 0xff),
                                  (byte)((rightColor & 0xff00) >> 8),
                                  (byte)((rightColor & 0xff0000) >> 0x10),
                                   (byte)((rightColor & -16777216) >> 0x18)
                                  );

                if (config.UseSystemButtonColor)
                {
                    buttonColor = new Color((byte)(colorButton & 0xff),
                                      (byte)((colorButton & 0xff00) >> 8),
                                      (byte)((colorButton & 0xff0000) >> 0x10),
                                       (byte)((colorButton & -16777216) >> 0x18)
                                      );
                    config.InactiveColor = buttonColor;
                    config.StickColor = GetStickColor(buttonColor);
                }

                Keys = BitConverter.ToUInt64(data, GetOffset(0, i));
                SetStickColor(HidNpadButton.StickL, 8, 7, -1);
                SetStickColor(HidNpadButton.StickR, 9, -1, 10);
                SetButtonColor(HidNpadButton.A, 10, -1, 3);
                SetButtonColor(HidNpadButton.Y, 13, -1, 4);
                SetButtonColor(HidNpadButton.B, 12, -1, 2);
                SetButtonColor(HidNpadButton.X, 11, -1, 5);
                SetButtonColor(HidNpadButton.Plus, 14, -1, 12);
                SetButtonColor(HidNpadButton.Minus, 15, 3, -1);
                SetButtonColor(HidNpadButton.R, 18, -1, 6);
                SetButtonColor(HidNpadButton.ZR, 16, -1, 11);
                SetButtonColor(HidNpadButton.L, 17, 12, -1);
                SetButtonColor(HidNpadButton.ZL, 1, 11, -1);
                SetButtonColor(HidNpadButton.Up, 3, 8, -1);
                SetButtonColor(HidNpadButton.Down, 5, 10, -1);
                SetButtonColor(HidNpadButton.Left, 6, 4, -1);
                SetButtonColor(HidNpadButton.Right, 4, 9, -1);

                if ((styleSet & Convert.ToInt32(HidNpadStyleTag.NpadJoyDual)) != 0 && config.EnableGyroModels)
                {
                    unsafe
                    {
                        if (config.UseSystemControllerColor)
                        {
                            joyLeftModel.materials[1].maps[0].color = controllerColorLeft;
                            joyLeftModel.materials[5].maps[0].color = controllerColorLeft;
                            joyLeftModel.materials[6].maps[0].color = controllerColorLeft;
                            joyRightModel.materials[7].maps[0].color = controllerColorRight;
                            joyRightModel.materials[8].maps[0].color = controllerColorRight;
                            joyRightModel.materials[9].maps[0].color = controllerColorRight;
                        }
                        joyLeftModel.materials[2].maps[0].color = config.InactiveColor;
                        joyRightModel.materials[1].maps[0].color = config.InactiveColor;
                    }


                    if (calibrationCooldown[i] > 0)
                    {
                        calibrationCooldown[i]--;

                        baseRotation[i] = Raymath.QuaternionSlerp(baseRotation[i], slerpTo[i], ((float)config.PacketsPerSecond / 2 - (float)calibrationCooldown[i]) / (float)config.PacketsPerSecond / 2);
                        baseRotation2[i] = Raymath.QuaternionSlerp(baseRotation2[i], slerpTo2[i], ((float)config.PacketsPerSecond / 2 - (float)calibrationCooldown[i]) / (float)config.PacketsPerSecond / 2);
                    }
                    if ((Keys & Convert.ToUInt64(HidNpadButton.StickL)) != 0 && calibrationCooldown[i] <= 0)
                    {
                        calibrationCooldown[i] = config.PacketsPerSecond / 2;
                        slerpTo[i] = GetControllerQuat(data, 0, i);
                        slerpTo2[i] = GetControllerQuat(data, 1, i);
                    }

                    Quaternion leftQuat = GetControllerQuat(data, 0, i);
                    joyLeftModel.transform = Raymath.QuaternionToMatrix(Raymath.QuaternionMultiply(Raymath.QuaternionInvert(baseRotation[i]), leftQuat));
                    Quaternion rightQuat = GetControllerQuat(data, 1, i);
                    joyRightModel.transform = Raymath.QuaternionToMatrix(Raymath.QuaternionMultiply(Raymath.QuaternionInvert(baseRotation2[i]), rightQuat));

                    Raylib.BeginTextureMode(motionTextures[i]);
                    Raylib.ClearBackground(config.BackgroundColor);
                    Raylib.BeginMode3D(camera);

                    Raylib.DrawModel(joyLeftModel, new Vector3(-4, 0, 0), 2, Color.WHITE);
                    Raylib.DrawModel(joyRightModel, new Vector3(4, 0, 0), 2, Color.WHITE);

                    Raylib.EndMode3D();
                    Raylib.EndTextureMode();
                }
                else if ((styleSet & Convert.ToInt32(HidNpadStyleTag.NpadFullKey)) != 0 && config.EnableGyroModels)
                {
                    unsafe
                    {
                        if (config.UseSystemControllerColor)
                            proControllerModel.materials[2].maps[0].color = controllerColorLeft;
                        proControllerModel.materials[7].maps[0].color = config.InactiveColor;
                    }

                    Quaternion quat = GetControllerQuat(data, 0, i);
                    proControllerModel.transform = Raymath.QuaternionToMatrix(Raymath.QuaternionMultiply(Raymath.QuaternionInvert(baseRotation[i]), quat));

                    if (calibrationCooldown[i] > 0)
                    {
                        calibrationCooldown[i]--;

                        baseRotation[i] = Raymath.QuaternionSlerp(baseRotation[i], slerpTo[i], ((float)config.PacketsPerSecond / 2 - (float)calibrationCooldown[i]) / (float)config.PacketsPerSecond / 2);
                    }
                    if ((Keys & Convert.ToUInt64(HidNpadButton.StickL)) != 0 && calibrationCooldown[i] <= 0)
                    {
                        calibrationCooldown[i] = config.PacketsPerSecond / 2;
                        slerpTo[i] = GetControllerQuat(data, 0, i);
                    }
                    Raylib.BeginTextureMode(motionTextures[i]);
                    Raylib.ClearBackground(config.BackgroundColor);
                    Raylib.BeginMode3D(camera);

                    Raylib.DrawModel(proControllerModel, new Vector3(0, 0, 0), 2, Color.WHITE);

                    Raylib.EndMode3D();
                    Raylib.EndTextureMode();
                }
            }

            Raylib.BeginDrawing();
            Raylib.ClearBackground(config.BackgroundColor);
            for (int i = 0; i < numControllers; i++)
            {
                int styleSet = BitConverter.ToInt32(data, GetOffset(232, i));
                int colorButton = BitConverter.ToInt32(data, GetOffset(220, i));
                int leftColor = BitConverter.ToInt32(data, GetOffset(216, i));
                int rightColor = BitConverter.ToInt32(data, GetOffset(224, i));
                controllerColorLeft = new Color((byte)(leftColor & 0xff),
                                  (byte)((leftColor & 0xff00) >> 8),
                                  (byte)((leftColor & 0xff0000) >> 0x10),
                                   (byte)((leftColor & -16777216) >> 0x18)
                                  );
                controllerColorRight = new Color((byte)(rightColor & 0xff),
                                  (byte)((rightColor & 0xff00) >> 8),
                                  (byte)((rightColor & 0xff0000) >> 0x10),
                                   (byte)((rightColor & -16777216) >> 0x18)
                                  );

                if (config.UseSystemButtonColor)
                {
                    buttonColor = new Color((byte)(colorButton & 0xff),
                                      (byte)((colorButton & 0xff00) >> 8),
                                      (byte)((colorButton & 0xff0000) >> 0x10),
                                       (byte)((colorButton & -16777216) >> 0x18)
                                      );
                    config.InactiveColor = buttonColor;
                    config.StickColor = GetStickColor(buttonColor);
                }

                if ((styleSet & Convert.ToInt32(HidNpadStyleTag.NpadJoyDual)) != 0 && config.EnableGyroModels)
                {
                    unsafe
                    {
                        if (config.UseSystemControllerColor)
                        {
                            joyLeftModel.materials[1].maps[0].color = controllerColorLeft;
                            joyLeftModel.materials[5].maps[0].color = controllerColorLeft;
                            joyLeftModel.materials[6].maps[0].color = controllerColorLeft;
                            joyRightModel.materials[7].maps[0].color = controllerColorRight;
                            joyRightModel.materials[8].maps[0].color = controllerColorRight;
                            joyRightModel.materials[9].maps[0].color = controllerColorRight;
                        }
                        joyLeftModel.materials[2].maps[0].color = config.InactiveColor;
                        joyRightModel.materials[1].maps[0].color = config.InactiveColor;
                    }
                }
                else if ((styleSet & Convert.ToInt32(HidNpadStyleTag.NpadFullKey)) != 0 && config.EnableGyroModels)
                {
                    unsafe
                    {
                        if (config.UseSystemControllerColor)
                            proControllerModel.materials[2].maps[0].color = controllerColorLeft;
                        proControllerModel.materials[7].maps[0].color = config.InactiveColor;
                    }
                }

                Raylib.DrawTextureRec(motionTextures[i].texture, new Rectangle(0, 0, 619, -360), new Vector2(619 * i, 0), Color.WHITE);
                Keys = BitConverter.ToUInt64(data, GetOffset(0, i));
                LeftStick = new Vector2((float)BitConverter.ToInt32(data, GetOffset(8, i)), (float)BitConverter.ToInt32(data, GetOffset(12, i)));
                RightStick = new Vector2((float)BitConverter.ToInt32(data, GetOffset(16, i)), (float)BitConverter.ToInt32(data, GetOffset(20, i)));

                DrawStick(new Vector2(619 * i + 100, 180), LeftStick, HidNpadButton.StickL);
                DrawStick(new Vector2(619 * i + 380, 270), RightStick, HidNpadButton.StickR);

                DrawButton(new Vector2(619 * i + 550, 180), 25, 25, HidNpadButton.A, "A", 1, -12, -19, 38);
                DrawButton(new Vector2(619 * i + 450, 180), 25, 25, HidNpadButton.Y, "Y", 1, -12, -19, 38);
                DrawButton(new Vector2(619 * i + 500, 230), 25, 25, HidNpadButton.B, "B", 1, -12, -19, 38);
                DrawButton(new Vector2(619 * i + 500, 130), 25, 25, HidNpadButton.X, "X", 1, -12, -19, 38);
                DrawButton(new Vector2(619 * i + 380, 130), 12, 12, HidNpadButton.Plus, "+", 1, -9, -19, 38);
                DrawButton(new Vector2(619 * i + 220, 130), 12, 12, HidNpadButton.Minus, "-", 1, -7, -21, 38);
                DrawButton(new Vector2(619 * i + 500, 70), 40, 15, HidNpadButton.R, "R", 1, -12, -20, 38);
                DrawButton(new Vector2(619 * i + 500, 30), 40, 15, HidNpadButton.ZR, "ZR", 1, -22, -19, 38);
                DrawButton(new Vector2(619 * i + 100, 70), 40, 15, HidNpadButton.L, "L", 1, -12, -20, 38);
                DrawButton(new Vector2(619 * i + 100, 30), 40, 15, HidNpadButton.ZL, "ZL", 1, -22, -19, 38);
                DrawButton(new Vector2(619 * i + 220, 240), 15, 15, HidNpadButton.Up, "", 0.5f, 0, 0, 38);
                DrawButton(new Vector2(619 * i + 220, 300), 15, 15, HidNpadButton.Down, "", 0.5f, 0, 0, 38);
                DrawButton(new Vector2(619 * i + 190, 270), 15, 15, HidNpadButton.Left, "", 0.5f, 0, 0, 38);
                DrawButton(new Vector2(619 * i + 250, 270), 15, 15, HidNpadButton.Right, "", 0.5f, 0, 0, 38);
                Raylib.DrawRectangleRounded(new Rectangle(619 * i + 205, 250, 15 * 2, 20 * 2), 0, 8, config.InactiveColor);
                Raylib.DrawRectangleRounded(new Rectangle(619 * i + 200, 255, 20 * 2, 15 * 2), 0, 8, config.InactiveColor);

                if (calibrationCooldown[i] > 0)
                {
                    Raylib.DrawTextEx(fatFont, "Recalibrating", new Vector2(619 * i + 180, 10), 38, 2, Color.WHITE);
                }
            }

            Raylib.EndDrawing();
        }

        Raylib.CloseWindow();
    }
}