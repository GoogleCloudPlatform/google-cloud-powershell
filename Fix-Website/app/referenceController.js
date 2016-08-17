var app = angular.module('powershellSite');

/* The controller that handles most information. */
app.controller('ReferenceCtrl', ['$scope', '$http', function($scope, $http) {
    /* The frame demonstrates what type of information should be displayed.
     * 1: The main screen for Google Cloud Powershell.
     * 2: The screen detailing the activeProduct's information.
     * 3: The screen detailing the information for the active cmdlet.
     * The Table Controller handles changing these values.
     */
    this.frame = 1;
    /* Either the active product, or the active cmdlet. */
    this.active = '';
    /* The active product. Used for table of contents expansion. */
    this.activeProduct = '';

    /* checkFrame allows angular to determine what information to display. */
    this.checkFrame = function(check) {
        return this.frame === check;
    };

    /* setFrame is called by TableController (and clicking links)
     * in order to make sure that the correct information is displayed
     * at any time.
     */
    this.setFrame = function(newFrame, newActive) {
        if (newFrame === 3 && this.frame != 3) {
            /* We want to preserve the active product so that
             * expanded product is correct.
             */
            this.activeProduct = this.active;
        }
        this.frame = newFrame;
        this.active = newActive;
    };

    $http.get('_data/cmdletsFull.json')
        .then(function(res) {
            $scope.products = res.data;
        });
}]);
