﻿using System;
using Data;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace UpamtiMe.Tests
{
    [TestClass]
    public class UnitTest1
    {
        [TestMethod]
        public void TestMethod1()
        {
            Data.DTOs.StatisctisByDays statisctis1 = Data.Users.GetStatisctisByDays(2, 1);
            Data.DTOs.StatisctisByDays statisctis7 = Data.Users.GetStatisctisByDays(2, 7);
            Data.DTOs.StatisctisByDays statisctisTotal = Data.Users.GetStatisctisByDays(2);

            Data.DTOs.StatisctisByDays aIb = statisctis1.Add(statisctis7);
            Data.DTOs.StatisctisByDays bIc = statisctis7.Add(statisctisTotal);
            int a = 5;
        }
    }
}
