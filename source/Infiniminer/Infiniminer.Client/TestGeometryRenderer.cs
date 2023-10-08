﻿/* ----------------------------------------------------------------------------
MIT License

Copyright (c) 2009 Zach Barth
Copyright (c) 2023 Christopher Whitley

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
---------------------------------------------------------------------------- */

using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Media;

namespace Infiniminer
{
    class GeometryDebugger
    {
        GraphicsDevice graphicsDevice;
        VertexDeclaration vertexDeclaration;
        Effect effect;

        public Matrix ViewMatrix = Matrix.Identity;
        public Matrix ProjectionMatrix = Matrix.Identity;

        public GeometryDebugger(GraphicsDevice graphicsDevice, Effect effect)
        {
            this.graphicsDevice = graphicsDevice;
            vertexDeclaration = new VertexDeclaration(VertexPositionColor.VertexDeclaration.GetVertexElements());
            this.effect = effect;
        }

        public void DrawSphere(Vector3 position, float radius, Color color)
        {
            VertexPositionColor[] sphereVertices = ConstructSphereVertices(position, radius, color);
            effect.CurrentTechnique = effect.Techniques["Colored"];
            effect.Parameters["World"].SetValue(Matrix.Identity);
            effect.Parameters["View"].SetValue(ViewMatrix);
            effect.Parameters["Projection"].SetValue(ProjectionMatrix);
            foreach (EffectPass pass in effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                graphicsDevice.RasterizerState = RasterizerState.CullNone;
                graphicsDevice.DrawUserPrimitives(PrimitiveType.TriangleList, sphereVertices, 0, sphereVertices.Length / 3);
            }
        }

        public void DrawLine(Vector3 posStart, Vector3 posEnd, Color color)
        {
            
        }

        public VertexPositionColor[] ConstructSphereVertices(Vector3 position, float radius, Color color)
        {
            VertexPositionColor[] vertices = new VertexPositionColor[3 * 8];
            VertexPositionColor top = new VertexPositionColor(Vector3.Up*radius+position, color);
            VertexPositionColor bottom = new VertexPositionColor(Vector3.Down * radius + position, color);
            VertexPositionColor left = new VertexPositionColor(Vector3.Left * radius + position, color);
            VertexPositionColor right = new VertexPositionColor(Vector3.Right * radius + position, color);
            VertexPositionColor back = new VertexPositionColor(Vector3.Backward * radius + position, color);
            VertexPositionColor front = new VertexPositionColor(Vector3.Forward * radius + position, color);

            // top
            vertices[0] = back;
            vertices[1] = top;
            vertices[2] = right;

            vertices[3] = right;
            vertices[4] = top;
            vertices[5] = front;

            vertices[6] = front;
            vertices[7] = top;
            vertices[8] = left;

            vertices[9] = left;
            vertices[10] = top;
            vertices[11] = back;

            // bottom
            vertices[12] = back;
            vertices[13] = right;
            vertices[14] = bottom;

            vertices[15] = right;
            vertices[16] = front;
            vertices[17] = bottom;

            vertices[18] = front;
            vertices[19] = left;
            vertices[20] = bottom;

            vertices[21] = left;
            vertices[22] = back;
            vertices[23] = bottom;

            return vertices;
        }
    }
}
