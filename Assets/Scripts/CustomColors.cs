using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class CustomColors {
    public class PlayerColor {
        public string name;
        public Color color;
        public PlayerColor(string name, Color32 color) {
            this.name = name;
            this.color = color;
        }
    }

    //Overalls colors
    public static PlayerColor[] Primary = new PlayerColor[] {
        new("Default", new(0, 0, 0, 0)),
        new("Red", new(212, 60, 14, 255)),
        new("Orange", new(251, 132, 0, 255)),
        new("Yellow", new(245, 188, 0, 255)),
        new("Green", new(131, 188, 59, 255)),
        new("Teal", new(0, 203, 162, 255)),
        new("Blue", new(42, 126, 255, 255)),
        new("Purple", new(78, 60, 255, 255)),
        new("Pink", new(212, 43, 255, 255)),
        new("Magenta", new(131, 16, 164, 255)),
        new("Black", new(96, 96, 96, 255))
    };
    //Shirt/Hat colors
    public static PlayerColor[] Secondary = new PlayerColor[] {
        new("Default", new(0, 0, 0, 0)),
        new("Red", new(137, 57, 57, 255)),
        new("Orange", new(145, 88, 55, 255)),
        new("Yellow", new(145, 128, 45, 255)),
        new("Green", new(49, 120, 67, 255)),
        new("Teal", new(34, 104, 90, 255)),
        new("Blue", new(29, 68, 132, 255)),
        new("Purple", new(58, 51, 132, 255)),
        new("Pink", new(135, 41, 107, 255)),
        new("Magenta", new(78, 30, 91, 255)),
        new("Black", new(38, 38, 38, 255))
    };
    
}
