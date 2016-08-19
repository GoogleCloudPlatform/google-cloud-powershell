(function() {
    var app = angular.module('powershellSite', ['ngRoute']);

    app.config(function($routeProvider, $locationProvider) {
        $routeProvider
            .when('/', {
                templateUrl: 'static/templates/home.html',
                controller: 'ReferenceCtrl',
                controllerAs: 'ref',
                reloadOnSearch: false
            })
            .when('/:product', {
                templateUrl: 'static/templates/product.html',
                controller: 'ReferenceCtrl',
                controllerAs: 'ref',
                reloadOnSearch: false
            })
            .when('/:product/:cmdlet', {
                templateUrl: 'static/templates/cmdlet.html',
                controller: 'ReferenceCtrl',
                controllerAs: 'ref',
                reloadOnSearch: false
            });

        // Configure html5 for better linking.
        $locationProvider.html5Mode(true);
    });

    /* TODO: Change templates from html to .ng */
    app.directive('contentTable', function() {
        return {
            restrict: 'E',
            templateUrl: 'static/templates/content-table.html',
            controller: 'TableCtrl',
            controllerAs: 'table'
        };
    });

})();
