const AesGcmHelper = {
    //tạo key từ pin
    async importKey(pin) {
        const enc = new TextEncoder();
        const hash = await window.crypto.subtle.digest("SHA-256", enc.encode(pin));
        return window.crypto.subtle.importKey(
            "raw",
            hash,
            { name: "AES-GCM" },
            false,
            ["encrypt", "decrypt"]
        );
    },

    async encrypt(text, pin) {
        try {
            const key = await this.importKey(pin);
            const iv = window.crypto.getRandomValues(new Uint8Array(12)); // IV chuẩn 12 bytes
            const enc = new TextEncoder();

            //encr
            const encryptedBuffer = await window.crypto.subtle.encrypt(
                { name: "AES-GCM", iv: iv },
                key,
                enc.encode(text)
            );

            //gộp IV + CipherText
            const resultBuffer = new Uint8Array(iv.byteLength + encryptedBuffer.byteLength);
            resultBuffer.set(iv, 0);
            resultBuffer.set(new Uint8Array(encryptedBuffer), iv.byteLength);
            
            return this.arrayBufferToBase64(resultBuffer);
        } catch (e) {
            console.error("Encryption failed:", e);
            throw e;
        }
    },
    
    async decrypt(base64Str, pin) {
        try {
            const dataBuffer = this.base64ToArrayBuffer(base64Str);
            const key = await this.importKey(pin);

            //tách IV (12 bytes đầu) và CipherText (phần còn lại)
            const iv = dataBuffer.slice(0, 12);
            const ciphertext = dataBuffer.slice(12);

            const decryptedBuffer = await window.crypto.subtle.decrypt(
                { name: "AES-GCM", iv: iv },
                key,
                ciphertext
            );

            const dec = new TextDecoder();
            return dec.decode(decryptedBuffer);
        } catch (e) {
            console.error("Decryption failed (Wrong PIN or corrupted data):", e);
            return null; //null nếu giải mã lỗi
        }
    },

    //chuyển đổi Buffer <-> Base64
    arrayBufferToBase64(buffer) {
        let binary = '';
        const bytes = new Uint8Array(buffer);
        const len = bytes.byteLength;
        for (let i = 0; i < len; i++) {
            binary += String.fromCharCode(bytes[i]);
        }
        return window.btoa(binary);
    },

    base64ToArrayBuffer(base64) {
        const binary_string = window.atob(base64);
        const len = binary_string.length;
        const bytes = new Uint8Array(len);
        for (let i = 0; i < len; i++) {
            bytes[i] = binary_string.charCodeAt(i);
        }
        return bytes.buffer;
    }
};