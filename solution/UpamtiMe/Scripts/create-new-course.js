var hideCategories = function(cat, subcat) {
  var catID = cat.children('option:selected').val();
  subcat.children().removeAttr('hidden');
  subcat.children(':not([data-catid="' + catID + '"])').attr('hidden', '');
}

$('#new-course-category > select').change(function() {
  hideCategories($(this), $('#new-course-subcategory > select'));
});

$('#create-new-course-button').click(function() {
  var name = $('#new-course-name').val();
  var cat = $('#new-course-category > select').children(':selected').val();
  var subcat = $('#new-course-subcategory > select').children(':selected').val();
  var error = "";
  if (name == "" || name == undefined)                      error += 'Kurs mora imati ime! ';
  if (cat == "0" || cat == "" || cat === undefined)         error += 'Kurs mora pripadati nekoj kategoriji! ';
  if (subcat == "0" || subcat == "" || subcat == undefined) error += 'Kurs mora pripadati nekoj podkategoriji! ';
  if (error == "") {
    return true;
  } else {
    alert(error);
    return false;
  }
});
