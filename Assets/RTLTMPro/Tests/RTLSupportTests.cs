//using System.Collections;
//using System.Collections.Generic;
//using System.Security.Policy;
//using NUnit.Framework;
//using RTLTMPro;
//using UnityEngine;
//using UnityEngine.TestTools;

//namespace Tests
//{
//    public class RTLSupportTests
//    {
//        [Test]
//        public void ArabicTextIsSuccessfulyConverted()
//        {
//            const string input = "هَذَا النَّص العربي";
//            const string expected = "ﻲﺑﺮﻌﻟا ﺺﱠﻨﻟا اَﺬَﻫ";

//            string result = RTLSupport.FixRTL(input, false, false, false);

//            Assert.AreEqual(expected, result);
//        }

//        [Test]
//        public void FarsiTextIsSuccessfulyConverted()
//        {
//            const string input = "متن فارسی";
//            const string expected = "ﯽﺳرﺎﻓ ﻦﺘﻣ";

//            string result = RTLSupport.FixRTL(input, false, false, true);

//            Assert.AreEqual(expected, result);
//        }

//        [Test]
//        public void TashkeelIsMaintainedInBeginingOfText()
//        {
//            const string input = "ِصبا";
//            const string expected = "ِﺎﺒﺻ";;

//            string result = RTLSupport.FixRTL(input, false, false, false);

//            Assert.AreEqual(expected, result);
//        }
        
//        [Test]
//        public void TashkeelIsMaintainedInMiddleOfText()
//        {
//            const string input = "مَرد";
//            const string expected = "دﺮَﻣ";

//            string result = RTLSupport.FixRTL(input, false, false, false);

//            Assert.AreEqual(expected, result);
//        }
        
//        [Test]
//        public void TashkeelIsMaintainedInEndOfText()
//        {
//            const string input = "صبحِ";
//            const string expected = "ِﺢﺒﺻ";

//            string result = RTLSupport.FixRTL(input, false, false, false);

//            Assert.AreEqual(expected, result);
//        }
//    }
//}