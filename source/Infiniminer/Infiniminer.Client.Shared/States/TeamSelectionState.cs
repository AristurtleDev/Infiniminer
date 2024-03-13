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

using StateMasher;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Infiniminer.States
{
    public class TeamSelectionState : State
    {
        SpriteBatch spriteBatch;
        Texture2D texMenu;
        Rectangle drawRect;
        string nextState = null;
        SpriteFont uiFont;
        bool canCancel = false;

        ClickRegion[] clkTeamMenu = new ClickRegion[2] {
            new ClickRegion(new Rectangle(229,156,572,190), "red"),
            new ClickRegion(new Rectangle(135,424,761,181), "blue")
        };

        public override void OnEnter(string oldState)
        {
            _SM.IsMouseVisible = true;

            spriteBatch = new SpriteBatch(_SM.GraphicsDevice);
            texMenu = _SM.Content.Load<Texture2D>("menus/tex_menu_team");

            drawRect = new Rectangle(_SM.GraphicsDevice.Viewport.Width / 2 - 1024 / 2,
                                     _SM.GraphicsDevice.Viewport.Height / 2 - 768 / 2,
                                     1024,
                                     1024);

            uiFont = _SM.Content.Load<SpriteFont>("font_04b08");

            if (oldState == "Infiniminer.States.MainGameState")
                canCancel = true;
        }

        public override void OnLeave(string newState)
        {

        }

        public override string OnUpdate(GameTime gameTime, KeyboardState keyState, MouseState mouseState)
        {
            // Do network stuff.
            (_SM as InfiniminerGame).UpdateNetwork(gameTime);

            return nextState;
        }

        public override void OnRenderAtEnter(GraphicsDevice graphicsDevice)
        {

        }

        public void QuickDrawText(SpriteBatch spriteBatch, string text, int y, Color color)
        {
            spriteBatch.DrawString(uiFont, text, new Vector2(_SM.GraphicsDevice.Viewport.Width / 2 - uiFont.MeasureString(text).X / 2, drawRect.Y + y), color);
        }

        public override void OnRenderAtUpdate(GraphicsDevice graphicsDevice, GameTime gameTime)
        {
            int redTeamCount = 0, blueTeamCount = 0;
            foreach (Player p in _P.playerList.Values)
            {
                if (p.Team == PlayerTeam.Red)
                    redTeamCount += 1;
                else if (p.Team == PlayerTeam.Blue)
                    blueTeamCount += 1;
            }

            spriteBatch.Begin(blendState: BlendState.AlphaBlend, sortMode: SpriteSortMode.Deferred);
            spriteBatch.Draw(texMenu, drawRect, Color.White);
            QuickDrawText(spriteBatch, "" + redTeamCount + " PLAYERS", 360, Defines.IM_RED);
            QuickDrawText(spriteBatch, "" + blueTeamCount + " PLAYERS", 620, Defines.IM_BLUE);
            spriteBatch.End();
        }

        public override void OnKeyDown(Keys key)
        {
            if (key == Keys.Escape && canCancel)
                nextState = "Infiniminer.States.MainGameState";
        }

        public override void OnKeyUp(Keys key)
        {

        }

        public override void OnMouseDown(MouseButton button, int x, int y)
        {
            x -= drawRect.X;
            y -= drawRect.Y;
            switch (ClickRegion.HitTest(clkTeamMenu, new Point(x, y)))
            {
                case "red":
                    if (_P.playerTeam == PlayerTeam.Red && canCancel)
                        nextState = "Infiniminer.States.MainGameState";
                    else
                    {
                        _P.SetPlayerTeam(PlayerTeam.Red);
                        nextState = "Infiniminer.States.ClassSelectionState";
                    }
                    _P.PlaySound(InfiniminerSound.ClickHigh);
                    break;
                case "blue":
                    if (_P.playerTeam == PlayerTeam.Blue && canCancel)
                        nextState = "Infiniminer.States.MainGameState";
                    else
                    {
                        _P.SetPlayerTeam(PlayerTeam.Blue);
                        nextState = "Infiniminer.States.ClassSelectionState";
                    }
                    _P.PlaySound(InfiniminerSound.ClickHigh);
                    break;
            }
        }

        public override void OnMouseUp(MouseButton button, int x, int y)
        {

        }

        public override void OnMouseScroll(int scrollDelta)
        {

        }
    }
}
