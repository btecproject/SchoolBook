const StorageHelper = {
    /**
     *
     * @param {string} key
     * @param {string} value 
     * @param {boolean} isPersistent - True:LocalStorage, False: Lưu phiên SessionStorage
     */
    set: function(key, value, isPersistent = false) {
        if (isPersistent) {
            localStorage.setItem(key, value);
            sessionStorage.removeItem(key);
        } else {
            sessionStorage.setItem(key, value);
            localStorage.removeItem(key);
        }
    },


    get: function(key) {
        //Ưu tiên lấy từ Session
        let value = sessionStorage.getItem(key);

        //không có -> tìm trong Local
        if (!value) {
            value = localStorage.getItem(key);
        }
        return value;
    },
    
    remove: function(key) {
        sessionStorage.removeItem(key);
        localStorage.removeItem(key);
    }
};