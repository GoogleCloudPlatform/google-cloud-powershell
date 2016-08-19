var app = angular.module('powershellSite');

/** Angular doesn't sort objects, only arrays, so this filter was made.
 * This filter takes in A JSON object of cmdlets for a product and
 * allows you to sort them based off a specific cmdlet field, such as
 * 'name' or 'syntax'.
 **/
app.filter('orderByCmdletProp', function() {
    return function(cmdlets, cmdletField, reverse) {
        var filtered = [];
        angular.forEach(cmdlets, function(cmdlet) {
            filtered.push(cmdlet);
        });
        filtered.sort(function(a, b) {
            return (a[cmdletField] > b[cmdletField] ? 1 : -1);
        });
        if (reverse) filtered.reverse();
        return filtered;
    };
});
