using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.UIElements.StyleSheets;
using UnityEditor.ShaderGraph.GraphUI;
using UIECursor = UnityEngine.UIElements.Cursor;

namespace UnityEditor.ShaderGraph.GraphUI.UnitTests
{
    // An instance of this class is meant to be used to send events to a particular editor window
    // The window provided in the constructor will be the window that all events are sent to using this instance
    public class TestEventHelpers
    {
        EditorWindow m_Window;

        public TestEventHelpers(EditorWindow targetWindow)
        {
            m_Window = targetWindow;
        }

        //-----------------------------------------------------------
        // MouseDown Event Helpers
        //-----------------------------------------------------------
        public void MouseDownEvent(Vector2 point, int count, MouseButton mouseButton = MouseButton.LeftMouse, EventModifiers eventModifiers = EventModifiers.None)
        {
            m_Window.SendEvent(
                new Event
                {
                    type = EventType.MouseDown,
                    mousePosition = point,
                    clickCount = count,
                    button = (int)mouseButton,
                    modifiers = eventModifiers
                });
        }

        public void MouseDownEvent(Vector2 point, MouseButton mouseButton = MouseButton.LeftMouse,
            EventModifiers eventModifiers = EventModifiers.None)
        {
            MouseDownEvent(point, 1, mouseButton, eventModifiers);
        }

        public void MouseDownEvent(VisualElement element, MouseButton mouseButton = MouseButton.LeftMouse, EventModifiers eventModifiers = EventModifiers.None)
        {
            MouseDownEvent(element.worldBound.center, mouseButton, eventModifiers);
        }

        //-----------------------------------------------------------
        // MouseUp Event Helpers
        //-----------------------------------------------------------
        public void MouseUpEvent(Vector2 point, int count, MouseButton mouseButton = MouseButton.LeftMouse, EventModifiers eventModifiers = EventModifiers.None)
        {
            m_Window.SendEvent(
                new Event
                {
                    type = EventType.MouseUp,
                    mousePosition = point,
                    clickCount = count,
                    button = (int)mouseButton,
                    modifiers = eventModifiers
                });
        }

        public void MouseUpEvent(Vector2 point, MouseButton mouseButton = MouseButton.LeftMouse,
            EventModifiers eventModifiers = EventModifiers.None)
        {
            MouseUpEvent(point, 1, mouseButton, eventModifiers);
        }

        public void MouseUpEvent(VisualElement element, MouseButton mouseButton = MouseButton.LeftMouse, EventModifiers eventModifiers = EventModifiers.None)
        {
            MouseUpEvent(element.worldBound.center, mouseButton, eventModifiers);
        }

        //-----------------------------------------------------------
        // MouseDown + Up Event Helpers (aka Click)
        //-----------------------------------------------------------
        public void MouseClickEvent(Vector2 point, MouseButton mouseButton = MouseButton.LeftMouse, EventModifiers eventModifiers = EventModifiers.None)
        {
            MouseDownEvent(point, 1, mouseButton, eventModifiers);
            MouseUpEvent(point, 1, mouseButton, eventModifiers);
        }

        //-----------------------------------------------------------
        // MouseDrag Event Helpers
        //-----------------------------------------------------------
        public void MouseDragEvent(Vector2 start, Vector2 end, MouseButton mouseButton = MouseButton.LeftMouse, EventModifiers eventModifiers = EventModifiers.None)
        {
            m_Window.SendEvent(
                new Event
                {
                    type = EventType.MouseDrag,
                    mousePosition = end,
                    button = (int)mouseButton,
                    delta = end - start,
                    modifiers = eventModifiers
                });
        }

        //-----------------------------------------------------------
        // MouseMove Event Helpers
        //-----------------------------------------------------------
        public void MouseMoveEvent(Vector2 start, Vector2 end, MouseButton mouseButton = MouseButton.LeftMouse, EventModifiers eventModifiers = EventModifiers.None)
        {
            m_Window.SendEvent(
                new Event
                {
                    type = EventType.MouseMove,
                    mousePosition = end,
                    button = (int)mouseButton,
                    delta = end - start,
                    modifiers = eventModifiers
                });
        }

        public void MouseDragEvent(VisualElement start, VisualElement end, MouseButton mouseButton = MouseButton.LeftMouse, EventModifiers eventModifiers = EventModifiers.None)
        {
            MouseDragEvent(start.worldBound.center, end.worldBound.center, mouseButton, eventModifiers);
        }

        //-----------------------------------------------------------
        // ScrollWheel Event Helpers
        //-----------------------------------------------------------
        public void ScrollWheelEvent(float scrollDelta, Vector2 mousePosition, EventModifiers eventModifiers = EventModifiers.None)
        {
            m_Window.SendEvent(
                new Event
                {
                    type = EventType.ScrollWheel,
                    modifiers = eventModifiers,
                    mousePosition = mousePosition,
                    delta = new Vector2(scrollDelta, scrollDelta)
                });
        }

        //-----------------------------------------------------------
        // Keyboard Event Helpers
        //-----------------------------------------------------------

        /* ---- Uses a directly specified keyCode, meant to be used for more obscure keyboard keys */
        public void SimulateKeyPress(KeyCode inputKey, bool sendTwice = true, bool sendKeyUp = true)
        {
            SendKeyDownEvent(inputKey, EventModifiers.None, sendTwice);
            if(sendKeyUp)
                SendKeyUpEvent(inputKey);
        }

        public void SendKeyDownEvent(
            KeyCode key = KeyCode.None,
            EventModifiers eventModifiers = EventModifiers.None,
            bool sendTwice = true)
        {
            // In Unity, key down are sent twice: once with keycode, once with character.

            m_Window.SendEvent(
                new Event
                {
                    type = EventType.KeyDown,
                    keyCode = key,
                    modifiers = eventModifiers
                });

            if(sendTwice)
                m_Window.SendEvent(
                    new Event
                    {
                        type = EventType.KeyDown,
                        character = (char)key,
                        modifiers = eventModifiers
                    });
        }

        public void SendKeyUpEvent(
            KeyCode key = KeyCode.None,
            EventModifiers eventModifiers = EventModifiers.None)
        {
            m_Window.SendEvent(
                new Event
                {
                    type = EventType.KeyUp,
                    keyCode = key,
                    modifiers = eventModifiers
                });
        }
        /* ----  */

        /* ---- Uses a specified string input, figures out what keyCode to use, meant to be used for more typical alpha-numeric input */
        public void SimulateKeyPress(string inputKey)
        {
            SendKeyDownEvent(inputKey);
            SendKeyUpEvent(inputKey);
        }

        public void SendKeyDownEvent(
            string keyChar,
            EventModifiers eventModifiers = EventModifiers.None)
        {
            // Builds event with correct keyCode
            var keyEvent = Event.KeyboardEvent(keyChar);
            keyEvent.type = EventType.KeyDown;
            keyEvent.modifiers = eventModifiers;
            m_Window.SendEvent(keyEvent);
        }

        public void SendKeyUpEvent(
            string keyChar,
            EventModifiers eventModifiers = EventModifiers.None)
        {
            // Builds event with correct keyCode
            var keyEvent = Event.KeyboardEvent(keyChar);
            keyEvent.type = EventType.KeyUp;
            keyEvent.modifiers = eventModifiers;
            m_Window.SendEvent(keyEvent);
        }

        /* ---- */

        public static void TestAllElements(VisualElement container, Action<VisualElement> test)
        {
            test(container);
            for (int i = 0; i < container.hierarchy.childCount; ++i)
            {
                var element = container.hierarchy[i];
                TestAllElements(element, test);
            }
        }

        public static Vector2 GetScreenPosition(EditorWindow parentWindow, VisualElement visualElement)
        {
            // WorldBound is the "global" xposition of this element, relative to the top-left corner of this editor window
            var screenPosition = visualElement.worldBound.position;
            // EditorWindow.position is the top-left position of the window in desktop-space
            //screenPosition = (screenPosition + parentWindow.position.position);
            // To account for 4k screens with virtual coordinates, need to be multiply by EditorGUI.pixelsPerPoint to get actual desktop pixels.
            //screenPosition *= EditorGUIUtility.pixelsPerPoint;
            return screenPosition;
        }

    }
}
