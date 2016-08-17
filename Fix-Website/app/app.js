(function() {
    var app = angular.module('powershellSite', []);

    /* TODO: Change templates from html to .ng */

    app.directive("contentTable", function(){
        return {
            restrict: 'E',
            templateUrl: "templates/content-table.html",
            controller: "TableCtrl",
            controllerAs: 'table'
        };
    });

    app.directive("infoZone", function () {
        return {
            restrict: 'E',
            templateUrl: "templates/info.html"
        };
    });

})();
