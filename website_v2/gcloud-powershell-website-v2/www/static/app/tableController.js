var app = angular.module('powershellSite');

/** The controller for the table of contents. **/
app.controller('TableCtrl', function($scope, $attrs) {
    /** Whether or not a product's information is expanded. **/
    this.expanded = false;
    /** What the current active product is. **/
    this.activeProduct = '';

    /** clickProduct is used when a product is clicked. **/
    this.clickProduct = function(name) {
        /** It either closes the current expansion and shows the home page **/
        if (this.activeProduct === name) {
            this.activeProduct = '';
        }
        /** Or it sets the information to be the information screen for
         * the applicable product.
         **/
        else {
            this.activeProduct = name;
        }
    };

    /** isExpanded just tells whether or not a product is expanded **/
    this.isExpanded = function(name) {
        return (this.activeProduct === name);
    };
});
