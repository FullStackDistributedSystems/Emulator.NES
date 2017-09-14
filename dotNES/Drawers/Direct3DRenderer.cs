﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Windows.Forms;
using SharpDX;
using SharpDX.Direct2D1;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using AlphaMode = SharpDX.Direct2D1.AlphaMode;
using Device = SharpDX.Direct3D11.Device;
using Factory = SharpDX.DXGI.Factory;
using System.Runtime.InteropServices;
using SharpDX.Mathematics.Interop;
using Resource = SharpDX.Direct3D11.Resource;

namespace dotNES.Drawers
{
    class Direct3DRenderer : IRenderer
    {
        Device device;
        SwapChain swapChain;
        RenderTarget d2dRenderTarget;
        Bitmap gameBitmap;
        RawRectangleF clientArea;

        private UI _ui;

        public override void InitRendering(UI ui)
        {
            if (ui == null) return;
            _ui = ui;
            ResizeRedraw = true;
            var desc = new SwapChainDescription
            {
                BufferCount = 1,
                ModeDescription = new ModeDescription(ClientSize.Width, ClientSize.Height, new Rational(60, 1),
                    Format.R8G8B8A8_UNorm),
                IsWindowed = true,
                OutputHandle = Handle,
                SampleDescription = new SampleDescription(1, 0),
                SwapEffect = SwapEffect.Discard,
                Usage = Usage.RenderTargetOutput
            };

            Device.CreateWithSwapChain(DriverType.Hardware,
                DeviceCreationFlags.BgraSupport,
                new[] {SharpDX.Direct3D.FeatureLevel.Level_10_0},
                desc,
                out device,
                out swapChain);

            var d2dFactory = new SharpDX.Direct2D1.Factory();

            Factory factory = swapChain.GetParent<Factory>();
            factory.MakeWindowAssociation(Handle, WindowAssociationFlags.IgnoreAll);

            Texture2D backBuffer = Resource.FromSwapChain<Texture2D>(swapChain, 0);

            Surface surface = backBuffer.QueryInterface<Surface>();

            d2dRenderTarget = new RenderTarget(d2dFactory, surface,
                new RenderTargetProperties(new PixelFormat(Format.Unknown, AlphaMode.Premultiplied)));

            var bitmapProperties = new BitmapProperties(new PixelFormat(Format.B8G8R8A8_UNorm, AlphaMode.Ignore));
            gameBitmap = new Bitmap(d2dRenderTarget, new Size2(UI.GameWidth, UI.GameHeight), bitmapProperties);

            clientArea = new RawRectangleF
            {
                Left = 0,
                Top = 0,
                Bottom = ClientSize.Height,
                Right = ClientSize.Width
            };

            factory.Dispose();
            surface.Dispose();
            backBuffer.Dispose();
            _ui.ready = true;
        }

        private void DisposeDirect3D()
        {
            if (_ui != null && _ui.ready)
            {
                _ui.ready = false;
                d2dRenderTarget.Dispose();
                swapChain.Dispose();
                device.Dispose();
                gameBitmap.Dispose();
            }
        }

        public void OnClosing(CancelEventArgs e)
        {
            DisposeDirect3D();
        }

        protected override void OnResize(EventArgs e)
        {
            DisposeDirect3D();
            InitRendering(_ui);
            base.OnResize(e);
        }

        public override void Draw()
        {
            if (_ui == null || !_ui.ready) return;

            d2dRenderTarget.BeginDraw();
            d2dRenderTarget.Clear(Color.Gray);

            if (_ui.gameStarted)
            {
                int stride = UI.GameWidth * 4;
                gameBitmap.CopyFromMemory(_ui.rawBitmap, stride);

                d2dRenderTarget.DrawBitmap(gameBitmap, clientArea, 1f,
                    _ui._filterMode == UI.FilterMode.Linear
                        ? BitmapInterpolationMode.Linear
                        : BitmapInterpolationMode.NearestNeighbor);
            }

            d2dRenderTarget.EndDraw();
            swapChain.Present(0, PresentFlags.None);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Draw();
            base.OnPaint(e);
        }
    }
}