﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Data;
using Data.DTOs;
using UpamtiMe.Extensions;
using UpamtiMe.Models;

namespace UpamtiMe.Controllers
{
    public class CoursesController : Controller
    {
        // GET: Courses
        public ActionResult Index()
        {
            return View();
        }

        public ActionResult Profile(int id)
        {
            int usrID = UserSession.GetUser().UserID;
            CourseProfileModel model;
            if (Users.enrolled(usrID, id))
            {
                model = CourseProfileModel.Load(id, usrID);
            }
            else
            {
                model = CourseProfileModel.Load(id);
            }
            
            return View(model);
        }

        public ActionResult Enroll(int id)
        {
            int usrID = UserSession.GetUser().UserID;
            Users.enroll(usrID, id);
            return RedirectToAction("Profile", new {id = id});
        }

        public ActionResult CreateNew()
        {
            Models.CreateNewCourseModel model = new CreateNewCourseModel();
            return View(model);
        }

        [HttpPost]
        public ActionResult CreateNew(Models.CreateNewCourseModel model)
        {
            if (ModelState.IsValid)
            {
                Course c = Data.Courses.addCourse(model.Name, model.CategoryID, model.SubcategoryID, model.NumberOfCards, model.CreatorID);
                RedirectToAction("EditCourse", new {id = c.courseID });
            }
            else
            {
                return Json(new { success = false, result = ModelState.Errors() });
            }

            return View(model);
        }

        public ActionResult EditCourse(int id)
        {
            try
            {
                Models.EditCourseModel model = Models.EditCourseModel.Load(id);
                return View(model);
            }
            catch(Exception e)
            {
                return RedirectToAction("Error", "Home");
            }
        }

        [HttpPost]
        public ActionResult EditCourse(EditCourseLevelsCards model)
        {
            try
            {
                int numAdded = 0;
                int numDeleted = 0;

                if (model.DeletedCards != null)
                {
                    Courses.deleteCards(model.DeletedCards);
                    numDeleted += model.DeletedCards.Count;
                }
                    
                if (model.DeletedLevels != null)
                    Courses.deleteLevels(model.DeletedLevels);

                if (model.EditedCards != null)
                {
                    Courses.editCards(model.EditedCards);
                    numAdded += (from a in model.EditedCards where a.CardID == -1 select a).Count();
                }
                    
                if (model.EditedLevels != null)
                    Courses.editLevels(model.CourseID,model.EditedLevels);

                int oldnum = Courses.getCardNuber(model.CourseID);
                int newnum = oldnum + numAdded - numDeleted;
                

                Courses.updateCourseInfo(model.CourseID, model.Name, model.CategoryID, model.SubcategoryID, newnum);

                return Json(new {success = true});
                return RedirectToAction("EditCourse", new { id = model.CourseID });
            }
            catch (Exception e)
            {
                return Json(new { success = false});
            }
        }
    }
}