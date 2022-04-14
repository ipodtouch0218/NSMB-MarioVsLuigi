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
        new("Red", new(229, 45, 0, 255)),
        new("Orange", new(229, 118, 0, 255)),
        new("Yellow", new(229, 212, 0, 255)),
        new("Green", new(100, 229, 0, 255)),
        new("Teal", new(0, 229, 179, 255)),
        new("Blue", new(0, 91, 229, 255)),
        new("Indigo", new(118, 45, 229, 255)),
        new("Pink", new(229, 61, 229, 255)),
        new("Purple", new(181, 22, 229, 255)),
        new("Black", new(96, 96, 96, 255))
    };
    //Shirt/Hat colors
    public static PlayerColor[] Secondary = new PlayerColor[] {
        new("Default", new(0, 0, 0, 0)),
        new("Red", new(153, 28, 0, 255)),
        new("Orange", new(153, 76, 0, 255)),
        new("Yellow", new(153, 140, 0, 255)),
        new("Green", new(68, 153, 0, 255)),
        new("Teal", new(0, 153, 117, 255)),
        new("Blue", new(0, 61, 153, 255)),
        new("Indigo", new(79, 30, 153, 255)),
        new("Pink", new(153, 41, 151, 255)),
        new("Purple", new(120, 15, 153, 255)),
        new("Black", new(38, 38, 38, 255))
    };
    
}
