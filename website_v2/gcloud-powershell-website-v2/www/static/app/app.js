(function() {
    /* This is the angular module for the website. */
    var app = angular.module('powershellSite', ['ngRoute']);

    /**
     * Ordering matters with the routeprovider, as we must populate parameters
     * according to the URL.
     */
    app.config(function($routeProvider, $locationProvider) {
        $routeProvider
            .when('/', {
                templateUrl: 'static/templates/home.html',

                reloadOnSearch: false
            })
            .when('/:product', {
                templateUrl: 'static/templates/product.html',

                reloadOnSearch: false
            })
            .when('/:product/:cmdlet', {
                templateUrl: 'static/templates/cmdlet.html',

                reloadOnSearch: false
            });

        /* Configure html5mode so that we can pull data from the URL. */
        $locationProvider.html5Mode(true);
    });

    /* TODO: Change templates from html to .ng */
    /* Directive for the table of contents.*/
    app.directive('contentTable', function() {
        return {
            restrict: 'E',
            templateUrl: 'static/templates/content-table.html',
            controller: 'TableController',
            controllerAs: 'tableCtrl',
            scope: {
                productInfo: '='
            }
        };
    });

    /* Directive for the parameter set tables. */
    app.directive('parameterTable', function() {
        return {
            restrict: 'E',
            templateUrl: 'static/templates/parameter-table.html'
        };
    });
})();
