mergeInto(LibraryManager.library, {
MobileCheck: function() {
        var userAgent = window.navigator.userAgent.toLowerCase();
        var mobilePattern = / android | iphone | ipad | ipod / i;
        return userAgent.search(mobilePattern) !== -1;
    },
});