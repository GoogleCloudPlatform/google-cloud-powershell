var app = angular.module('powershellSite')

/* The controller for the table of contents. */
app.controller('TableCtrl', function ($scope, $attrs) {
    /* Whether or not a product's information is expanded. */
    this.expanded = false;
    /* What the current active product is. */
    this.activeProduct = "";

    /* clickProduct is used when a product is clicked. */
    this.clickProduct = function (name) {
        /* It either closes the current expansion and shows the home page */
        if (this.activeProduct === name) {
            $scope.ref.setFrame(1, "");
            this.activeProduct = "";
        }
        /* Or it sets the information to be the information screen for 
         * the applicable product.
         */
        else {
            $scope.ref.setFrame(2, name);
            this.activeProduct = name;
        }
    };

    /* clickCmdlet sets the information to be the information screen for 
     * the clicked cmdlet
     */
    this.clickCmdlet = function (name) {
        $scope.ref.setFrame(3, name);
    };

    /* isExpanded just tells the website whether or not a product is expanded*/
    this.isExpanded = function (name) {
        return (this.activeProduct === name);
    }
});
