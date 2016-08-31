var app = angular.module('powershellSite');

/**
 * The main controller for the app.
 * Sets up the productInfo object.
 */
app.controller('MainController',
    function($scope, $http, $routeParams) {
      this.productInfo = {};
      this.loading = true;
      this.errorLoading = false;
      try {
        /* We need to load the json and verify routeparameters. */
        var promise = $http.get('static/_data/cmdletsFull.json');
        promise.then(function(res) {
          /* We store the json info so templates have access */
          this.productInfo = res.data;
          this.loading = false;

          /* We make sure no invalid routes were passed in */
          if (Object.keys($routeParams).length === 2 && (
              !($routeParams.product in this.productInfo) ||
              !($routeParams.cmdlet in
              this.productInfo[$routeParams.product]))) {
                console.error('Invalid Product or Cmdlet');
                $routeParams.product = undefined;
                return;
          }
          else if (Object.keys($routeParams).length === 1 &&
              !($routeParams.product in this.productInfo)) {
                console.error('Invalid Product');
                $routeParams.product = undefined;
                return;
          }
        }.bind(this));
      } catch (err) {
        console.error(err);
        this.loading = false;
        this.errorLoading = true;
      }
      this.params = $routeParams;
      /**
       * The order we want cmdlet information to be displayed in.
       * Can be changed
       */
      this.order = ['synopsis', 'syntax', 'description', 'parameters',
          'examples', 'inputs', 'outputs'];

      /* Tells us if the object passed in is empty */
      this.isEmpty = function(linkObject) {
        return (Object.keys(linkObject).length === 0);
      };

      /* Gets the relevant cmdlet property from the productInfo */
      this.getProperty = function(property) {
        return this.productInfo[$routeParams.product][$routeParams.cmdlet][property];
      };
});
