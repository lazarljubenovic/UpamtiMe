﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using Data;
using Data.DTOs;

namespace UpamtiMe.Models
{
    public class PartialModel
    {
        public int UserID { get; set; }
        public string Username { get; set; }
        public string Name { get; set; }
        public byte[] Avatar { get; set; }
        public int Streak { get; set; }
        public float Score { get; set; }
        public int TotalCardsSeen { get; set; }
       
        public Data.DTOs.StatisctisByDays Statistics { get; set; }

        public List<Data.DTOs.LeaderboardEntryDTO> Leaderboard { get; set; }
        public LearningStatisticsDTO LearningStatistics { get; set; }

        public static PartialModel Load(int userID, LearningStatisticsDTO learningStatistics = null)
        {
            PartialModel returnValue = new PartialModel();
            User usr = Data.Users.GetUser(userID);

            returnValue.UserID = usr.userID;
            returnValue.Username = usr.username;
            returnValue.Name = usr.name;
            returnValue.Avatar = usr.avatar?.ToArray();
            returnValue.Streak = usr.streak;
            returnValue.Score = usr.score;
            returnValue.TotalCardsSeen = usr.totalCardsSeen;
           
            returnValue.Statistics = Data.Users.GetStatisctisByDays(userID, timeSpan: 7);

            returnValue.Leaderboard = Data.Users.getLeaderboard(userID);
            if(learningStatistics == null)
                returnValue.LearningStatistics = Data.Courses.getUserLearningStatistics(userID);
            else
            {
                returnValue.LearningStatistics = learningStatistics;
            }

            return returnValue;
        }


    }
}