using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input.Oculus;

namespace Infiniminer
{
    public class Infiniminer3DVRGame : InfiniminerGame
    {
        SpriteBatch spriteBatch;
        BasicEffect spriteBatchEffect;

        OvrDevice _ovrDevice;


        public Infiniminer3DVRGame(string[] args)
            : base(args)
        {
            // 90Hz Frame rate for oculus
            TargetElapsedTime = TimeSpan.FromTicks(111111);
            IsFixedTimeStep = true;
            // disable PC monitor VSync
            graphicsDeviceManager.SynchronizeWithVerticalRetrace = false;

            // we don't care is the main window is Focuses or not
            // because we render on the Oculus surface.
            InactiveSleepTime = TimeSpan.FromSeconds(0);

            // OVR requirees at least DX feature level 10.0
            graphicsDeviceManager.GraphicsProfile = GraphicsProfile.FL10_0;

            // create oculus device
            _ovrDevice = new OvrDevice(graphicsDeviceManager);
        }

        protected override void LoadContent()
        {
            spriteBatch = new SpriteBatch(GraphicsDevice);
            spriteBatchEffect = new BasicEffect(GraphicsDevice);
            spriteBatchEffect.TextureEnabled = true;
            spriteBatchEffect.VertexColorEnabled = true;

            base.LoadContent();
        }

        protected override void Update(GameTime gameTime)
        {
            // initialize ovrDevice
            if (!_ovrDevice.IsConnected)
            {
                try
                {
                    // Initialize Oculus VR
                    int ovrCreateResult = _ovrDevice.CreateDevice();
                    if (ovrCreateResult == 0)
                    {

                    }
                }
                catch (Exception ovre)
                {
                    System.Diagnostics.Debug.WriteLine(ovre.Message);
                }
            }

            base.Update(gameTime);
        }


        /// <summary>
        /// This is called when the game should draw itself.
        /// </summary>
        protected override void Draw(GameTime gameTime)
        {
            Vector3 cameraPosition = new Vector3(0, 0, 2.5f);
            float aspect = GraphicsDevice.Viewport.AspectRatio;
            Matrix view = Matrix.CreateLookAt(cameraPosition, Vector3.Zero, Vector3.Up);
            Matrix projection = Matrix.CreatePerspectiveFieldOfView(1, aspect, 1, 10);

            if (_ovrDevice.IsConnected)
            {
                // draw on VR headset
                int ovrResult = _ovrDevice.BeginFrame();
                if (ovrResult >= 0)
                {
                    HeadsetState headsetState = _ovrDevice.GetHeadsetState();
                    Matrix headTransform = headsetState.HeadTransform;

                    this.propertyBag.playerCamera.ApplyHeadTransform(headTransform);

                    // draw each eye on a rendertarget
                    for (int eye = 0; eye < 2; eye++)
                    {
                        RenderTarget2D rt = _ovrDevice.GetEyeRenderTarget(eye);
                        GraphicsDevice.SetRenderTarget(rt);
                        if (this.propertyBag.blockEngine.bloomPosteffect != null)
                            this.propertyBag.blockEngine.bloomPosteffect.DefaultBackBuffer = rt;

                        cameraPosition = this.propertyBag.playerCamera.Position;
                        Matrix globalWorld = Matrix.CreateRotationY(this.propertyBag.playerCamera.Yaw)
                                           * Matrix.CreateTranslation(cameraPosition)
                                           * Matrix.CreateTranslation(0.0f, -0.3f, 0.0f)
                                           ;

                        // VR eye view and projection
                        Matrix eyeTransform = headsetState.GetEyeTransform(eye);
                        view = headsetState.GetEyeView(eye);
                        view = Matrix.Invert(eyeTransform);

                        projection = _ovrDevice.CreateProjection(eye, 0.01f, 1000.0f);

                        this.propertyBag.playerCamera.UseVrCamera = true;
                        this.propertyBag.playerCamera.ViewMatrix = Matrix.Invert(globalWorld) * view;
                        this.propertyBag.playerCamera.ProjectionMatrix = projection;

                        DrawScene(gameTime, view, projection);

                        // Resolve eye rendertarget
                        GraphicsDevice.SetRenderTarget(null);
                        if (this.propertyBag.blockEngine.bloomPosteffect != null)
                            this.propertyBag.blockEngine.bloomPosteffect.DefaultBackBuffer = null;
                        // submit eye rendertarget
                        _ovrDevice.CommitRenderTarget(eye, rt);
                    }

                    // submit frame
                    int result = _ovrDevice.EndFrame();

                    // draw on PC screen
                    GraphicsDevice.SetRenderTarget(null);
                    if (this.propertyBag.blockEngine.bloomPosteffect != null)
                        this.propertyBag.blockEngine.bloomPosteffect.DefaultBackBuffer = null;

                    this.propertyBag.playerCamera.ApplyHeadTransform(Matrix.Identity);
                    this.propertyBag.playerCamera.UseVrCamera = false;

                    view = Matrix.CreateLookAt(cameraPosition, Vector3.Zero, Vector3.Up);
                    projection = Matrix.CreatePerspectiveFieldOfView(1, aspect, 1, 10);
                    DrawScene(gameTime, view, projection);

                    this.propertyBag.playerCamera.UseVrCamera = true;
                    this.propertyBag.playerCamera.ApplyHeadTransform(headTransform);

                    // preview VR rendertargets
                    //GraphicsDevice.Clear(Color.Black);
                    //var pp = GraphicsDevice.PresentationParameters;
                    //int height = pp.BackBufferHeight;
                    //float aspectRatio = (float)ovrDevice.GetEyeRenderTarget(0).Width / ovrDevice.GetEyeRenderTarget(0).Height;

                    //int width = Math.Min(pp.BackBufferWidth, (int)(height * aspectRatio));
                    //spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive);
                    //spriteBatch.Draw(ovrDevice.GetEyeRenderTarget(0), new Rectangle(0, 0, width, height), Color.White);
                    //spriteBatch.Draw(ovrDevice.GetEyeRenderTarget(1), new Rectangle(width, 0, width, height), Color.White);
                    //spriteBatch.End();

                    return;
                }
            }

            // draw on PC screen
            GraphicsDevice.SetRenderTarget(null);
            if (this.propertyBag.blockEngine.bloomPosteffect != null)
                this.propertyBag.blockEngine.bloomPosteffect.DefaultBackBuffer = null;

            DrawScene(gameTime, view, projection);
        }

        private void DrawScene(GameTime gameTime, Matrix view, Matrix projection)
        {
            Matrix cameraMtx = Matrix.Invert(view);
            spriteBatchEffect.World = Matrix.CreateConstrainedBillboard(
                    Vector3.Zero, cameraMtx.Translation, Vector3.UnitY, null, Vector3.Forward);
            spriteBatchEffect.View = view;
            spriteBatchEffect.Projection = projection;

            // draw any drawable components
            base.Draw(gameTime);
        }

    }


}
