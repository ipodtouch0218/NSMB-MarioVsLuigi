using UnityEngine;

public static class CustomColors {
    public class PlayerColor {
        public string name;
        public Color hat, overalls;
        public PlayerColor(string name, Color32 hat, Color32 overalls) {
            this.name = name;
            this.hat = hat;
            this.overalls = overalls;
        }
    }

    public static PlayerColor[] Colors = new PlayerColor[] {
                            //HAT                      //OVERALLS
        new("Default",      new(0, 0, 0, 0),            new(0, 0, 0, 0)        ),
        new("Blue",         new(74, 68, 244, 255),      new(68, 150, 246, 255) ),
        new("Wario",        new(255, 223, 70, 255),     new(74, 14, 127, 255)  ),
        new("Waluigi",      new(74, 14, 127, 255),      new(46, 40, 51, 255)   ),
        new("Pink",         new(255, 110, 170, 255),    new(255, 215, 110, 255)),
        new("Orange",       new(242, 124, 42, 255),     new(99, 212, 80, 255)  ),
        new("Maroon",       new(126, 39, 57, 255),      new(236, 171, 171, 255)),
        new("Reverse",      new(66, 66, 255, 255),      new(234, 40, 60, 255)  ),
        new("SMW",          new(253, 61, 112, 255),     new(89, 225, 211, 255) ),
        new("SMB3",         new(250, 57, 34, 255),      new(31, 31, 40, 255)   ),
        new("Builder",      new(255, 202, 43, 255),     new(234, 40, 60, 255)  ),
        new("Crazy Cap",    new(72, 0, 144, 255),       new(221, 164, 0, 255)  ),
        new("Spike",        new(72, 32, 32, 255),       new(183, 143, 111, 255)),
        new("AnimeLuigi",   new(13, 71, 123, 255),      new(242, 181, 80, 255) ),
        new("Mindnomad",    new(140, 24, 156, 255),     new(186, 186, 186, 255)),
        new("Chibi",        new(255, 255, 0, 255),      new(54, 54, 54, 255)   ),

        new("Strawberry",   new(255, 112, 205, 255),    new(184, 0, 0, 255)    ),
        new("Metal",        new(169, 169, 169, 255),    new(102, 102, 102, 255)),
        new("Cotton Candy", new(255, 127, 237, 255),    new(0, 148, 255, 255)  ),
        new("Mud",          new(76, 103, 88, 255),      new(34, 34, 84, 255)   ),

    };
}

