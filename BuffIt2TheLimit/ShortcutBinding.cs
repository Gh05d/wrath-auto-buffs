using BuffIt2TheLimit.Config;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace BuffIt2TheLimit {

    [JsonConverter(typeof(ShortcutBindingConverter))]
    public readonly struct ShortcutBinding {
        public readonly KeyCode Key;
        public readonly bool Ctrl;
        public readonly bool Shift;
        public readonly bool Alt;

        public ShortcutBinding(KeyCode key, bool ctrl = false, bool shift = false, bool alt = false) {
            Key = key;
            Ctrl = ctrl;
            Shift = shift;
            Alt = alt;
        }

        public bool IsNone => Key == KeyCode.None;

        public bool IsPressed() {
            if (Key == KeyCode.None) return false;
            if (!Input.GetKeyDown(Key)) return false;
            bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            bool alt = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
            return Ctrl == ctrl && Shift == shift && Alt == alt;
        }

        public static ShortcutBinding None => new(KeyCode.None);

        public static ShortcutBinding Capture(KeyCode key) {
            return new ShortcutBinding(
                key,
                ctrl: Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl),
                shift: Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift),
                alt: Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt)
            );
        }

        public string ToDisplayString() {
            if (Key == KeyCode.None) return "shortcut.none".i8();
            var parts = new List<string>();
            if (Ctrl) parts.Add("Ctrl");
            if (Shift) parts.Add("Shift");
            if (Alt) parts.Add("Alt");
            parts.Add(Key.ToString());
            return string.Join("+", parts);
        }
    }

    public class ShortcutBindingConverter : JsonConverter {
        public override bool CanConvert(Type objectType) => objectType == typeof(ShortcutBinding);

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer) {
            if (reader.TokenType == JsonToken.String) {
                // Old format: bare KeyCode string like "F5"
                var str = (string)reader.Value;
                if (Enum.TryParse<KeyCode>(str, out var kc))
                    return new ShortcutBinding(kc);
                return ShortcutBinding.None;
            }
            if (reader.TokenType == JsonToken.StartObject) {
                var obj = JObject.Load(reader);
                var key = KeyCode.None;
                if (obj.TryGetValue("Key", out var keyToken) && Enum.TryParse<KeyCode>(keyToken.ToString(), out var parsed))
                    key = parsed;
                bool ctrl = obj.Value<bool?>("Ctrl") ?? false;
                bool shift = obj.Value<bool?>("Shift") ?? false;
                bool alt = obj.Value<bool?>("Alt") ?? false;
                return new ShortcutBinding(key, ctrl, shift, alt);
            }
            return ShortcutBinding.None;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) {
            var binding = (ShortcutBinding)value;
            writer.WriteStartObject();
            writer.WritePropertyName("Key");
            writer.WriteValue(binding.Key.ToString());
            writer.WritePropertyName("Ctrl");
            writer.WriteValue(binding.Ctrl);
            writer.WritePropertyName("Shift");
            writer.WriteValue(binding.Shift);
            writer.WritePropertyName("Alt");
            writer.WriteValue(binding.Alt);
            writer.WriteEndObject();
        }
    }
}
