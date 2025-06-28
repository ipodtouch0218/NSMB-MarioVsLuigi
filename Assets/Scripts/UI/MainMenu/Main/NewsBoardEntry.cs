using System;
using TMPro;
using UnityEngine;

namespace NSMB.UI.MainMenu.Submenus.Main {
    public class NewsBoardEntry : MonoBehaviour {

        //---Serialized Variables
        [SerializeField] private TMP_Text title, author, body;

        public void Initialize(NewsBoardData data) {
            title.text = data.Title;
            author.text = DateTimeOffset.FromUnixTimeSeconds(data.Created).ToLocalTime().DateTime.ToString() + " - " + data.Author;
            body.text = data.Text;
            gameObject.SetActive(true);
        }


        [Serializable]
        public class NewsBoardData {
            public int Id { get; set; }
            public string Title { get; set; }
            public string Author { get; set; }
            public long Created { get; set; }
            public string Text { get; set; }
        }
    }
}