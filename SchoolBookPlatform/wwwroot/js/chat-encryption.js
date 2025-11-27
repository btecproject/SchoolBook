class ChatEncryption {
    constructor() {
        this.keyPair = null;             
        this.threadKeys = new Map();      
        this.publicKeys = new Map();   
    }

    // ===================================================
    // 1. KEY GENERATION
    // ===================================================

    /**
     * Generate RSA key pair for user
     * @returns {Promise<{publicKey: CryptoKey, privateKey: CryptoKey}>}
     */
    async generateKeyPair() {
        try {
            console.log(' Generating RSA key pair...');

            const keyPair = await window.crypto.subtle.generateKey(
                {
                    name: "RSA-OAEP",
                    modulusLength: 2048,
                    publicExponent: new Uint8Array([1, 0, 1]),
                    hash: "SHA-256"
                },
                true,  // extractable
                ["encrypt", "decrypt"]
            );

            this.keyPair = keyPair;
            console.log(' RSA key pair generated');

            return keyPair;
        } catch (error) {
            console.error(' Key pair generation failed:', error);
            throw error;
        }
    }

    /**
     * Generate AES key for thread/message
     * @returns {Promise<CryptoKey>}
     */
    async generateAESKey() {
        try {
            const key = await window.crypto.subtle.generateKey(
                {
                    name: "AES-GCM",
                    length: 256
                },
                true,  // extractable
                ["encrypt", "decrypt"]
            );

            return key;
        } catch (error) {
            console.error(' AES key generation failed:', error);
            throw error;
        }
    }

    // ===================================================
    // 2. KEY MANAGEMENT
    // ===================================================

    /**
     * Export public key to base64 string
     * @param {CryptoKey} publicKey
     * @returns {Promise<string>}
     */
    async exportPublicKey(publicKey) {
        const exported = await window.crypto.subtle.exportKey("spki", publicKey);
        const exportedAsBase64 = btoa(String.fromCharCode(...new Uint8Array(exported)));
        return exportedAsBase64;
    }

    /**
     * Import public key from base64 string
     * @param {string} base64Key
     * @returns {Promise<CryptoKey>}
     */
    async importPublicKey(base64Key) {
        const binaryKey = Uint8Array.from(atob(base64Key), c => c.charCodeAt(0));
        const publicKey = await window.crypto.subtle.importKey(
            "spki",
            binaryKey,
            {
                name: "RSA-OAEP",
                hash: "SHA-256"
            },
            true,
            ["encrypt"]
        );
        return publicKey;
    }

    /**
     * Export private key to encrypted base64 string
     * @param {CryptoKey} privateKey
     * @param {string} password - User's password
     * @returns {Promise<{encryptedKey: string, salt: string}>}
     */
    async exportPrivateKey(privateKey, password) {
        // Export private key
        const exported = await window.crypto.subtle.exportKey("pkcs8", privateKey);

        // Derive encryption key from password
        const salt = window.crypto.getRandomValues(new Uint8Array(16));
        const passwordKey = await this.deriveKeyFromPassword(password, salt);

        // Encrypt private key with password
        const iv = window.crypto.getRandomValues(new Uint8Array(12));
        const encrypted = await window.crypto.subtle.encrypt(
            { name: "AES-GCM", iv: iv },
            passwordKey,
            exported
        );

        // Combine IV + encrypted data
        const combined = new Uint8Array(iv.length + encrypted.byteLength);
        combined.set(iv);
        combined.set(new Uint8Array(encrypted), iv.length);

        return {
            encryptedKey: btoa(String.fromCharCode(...combined)),
            salt: btoa(String.fromCharCode(...salt))
        };
    }

    /**
     * Import private key from encrypted base64 string
     * @param {string} encryptedKeyBase64
     * @param {string} saltBase64
     * @param {string} password
     * @returns {Promise<CryptoKey>}
     */
    async importPrivateKey(encryptedKeyBase64, saltBase64, password) {
        // Decode
        const combined = Uint8Array.from(atob(encryptedKeyBase64), c => c.charCodeAt(0));
        const salt = Uint8Array.from(atob(saltBase64), c => c.charCodeAt(0));

        // Extract IV and encrypted data
        const iv = combined.slice(0, 12);
        const encrypted = combined.slice(12);

        // Derive decryption key from password
        const passwordKey = await this.deriveKeyFromPassword(password, salt);

        // Decrypt private key
        const decrypted = await window.crypto.subtle.decrypt(
            { name: "AES-GCM", iv: iv },
            passwordKey,
            encrypted
        );

        // Import decrypted private key
        const privateKey = await window.crypto.subtle.importKey(
            "pkcs8",
            decrypted,
            {
                name: "RSA-OAEP",
                hash: "SHA-256"
            },
            true,
            ["decrypt"]
        );

        return privateKey;
    }

    /**
     * Derive encryption key from password using PBKDF2
     */
    async deriveKeyFromPassword(password, salt) {
        const encoder = new TextEncoder();
        const passwordKey = await window.crypto.subtle.importKey(
            "raw",
            encoder.encode(password),
            "PBKDF2",
            false,
            ["deriveBits", "deriveKey"]
        );

        return await window.crypto.subtle.deriveKey(
            {
                name: "PBKDF2",
                salt: salt,
                iterations: 100000,
                hash: "SHA-256"
            },
            passwordKey,
            { name: "AES-GCM", length: 256 },
            true,
            ["encrypt", "decrypt"]
        );
    }

    // ===================================================
    // 3. MESSAGE ENCRYPTION
    // ===================================================

    /**
     * Encrypt message content
     * @param {string} message - Plain text message
     * @param {number} threadId
     * @returns {Promise<{encryptedContent, iv, encryptedKey}>}
     */
    async encryptMessage(message, threadId) {
        try {
            console.log(' Encrypting message...');

            // Generate AES key for this message
            const messageKey = await this.generateAESKey();

            // Generate IV
            const iv = window.crypto.getRandomValues(new Uint8Array(12));

            // Encrypt message with AES
            const encoder = new TextEncoder();
            const encrypted = await window.crypto.subtle.encrypt(
                {
                    name: "AES-GCM",
                    iv: iv
                },
                messageKey,
                encoder.encode(message)
            );

            // Export message key
            const exportedKey = await window.crypto.subtle.exportKey("raw", messageKey);

            // Encrypt message key with user's public key (for storage)
            // In real implementation, encrypt with each recipient's public key
            const encryptedKey = await this.encryptKeyWithPublicKey(
                new Uint8Array(exportedKey),
                this.keyPair.publicKey
            );

            console.log(' Message encrypted');

            return {
                encryptedContent: btoa(String.fromCharCode(...new Uint8Array(encrypted))),
                iv: btoa(String.fromCharCode(...iv)),
                encryptedKey: btoa(String.fromCharCode(...new Uint8Array(encryptedKey)))
            };
        } catch (error) {
            console.error(' Encryption failed:', error);
            throw error;
        }
    }

    /**
     * Decrypt message content
     * @param {string} encryptedContentBase64
     * @param {string} ivBase64
     * @param {string} encryptedKeyBase64
     * @returns {Promise<string>} - Decrypted plain text
     */
    async decryptMessage(encryptedContentBase64, ivBase64, encryptedKeyBase64) {
        try {
            console.log(' Decrypting message...');

            // Decode from base64
            const encryptedContent = Uint8Array.from(atob(encryptedContentBase64), c => c.charCodeAt(0));
            const iv = Uint8Array.from(atob(ivBase64), c => c.charCodeAt(0));
            const encryptedKey = Uint8Array.from(atob(encryptedKeyBase64), c => c.charCodeAt(0));

            // Decrypt message key with private key
            const decryptedKey = await window.crypto.subtle.decrypt(
                { name: "RSA-OAEP" },
                this.keyPair.privateKey,
                encryptedKey
            );

            // Import decrypted AES key
            const messageKey = await window.crypto.subtle.importKey(
                "raw",
                decryptedKey,
                "AES-GCM",
                true,
                ["decrypt"]
            );

            // Decrypt message content
            const decrypted = await window.crypto.subtle.decrypt(
                {
                    name: "AES-GCM",
                    iv: iv
                },
                messageKey,
                encryptedContent
            );

            const decoder = new TextDecoder();
            const plainText = decoder.decode(decrypted);

            console.log(' Message decrypted');
            return plainText;

        } catch (error) {
            console.error(' Decryption failed:', error);
            return '[Encrypted Message - Cannot Decrypt]';
        }
    }

    /**
     * Encrypt AES key with RSA public key
     */
    async encryptKeyWithPublicKey(keyData, publicKey) {
        return await window.crypto.subtle.encrypt(
            { name: "RSA-OAEP" },
            publicKey,
            keyData
        );
    }

    // ===================================================
    // 4. HELPER METHODS
    // ===================================================

    /**
     * Store keys in browser (IndexedDB or localStorage)
     * WARNING: For demo only, use secure storage in production
     */
    async storeKeysLocally() {
        if (!this.keyPair) {
            throw new Error('No key pair to store');
        }

        const publicKeyBase64 = await this.exportPublicKey(this.keyPair.publicKey);

        // Store public key (can be shared)
        localStorage.setItem('chat_public_key', publicKeyBase64);

        console.log(' Public key stored locally');
        
    }

    /**
     * Load keys from local storage
     */
    async loadKeysLocally() {
        const publicKeyBase64 = localStorage.getItem('chat_public_key');

        if (publicKeyBase64) {
            const publicKey = await this.importPublicKey(publicKeyBase64);
            console.log('Public key loaded from storage');
            
        }
    }

    /**
     * Initialize encryption for user
     */
    async initialize() {
        console.log(' Initializing chat encryption...');

        // Check if keys exist in storage
        const existingPublicKey = localStorage.getItem('chat_public_key');

        if (!existingPublicKey) {
            // Generate new key pair
            await this.generateKeyPair();
            await this.storeKeysLocally();

            // TODO: Upload public key to server
            console.log('New keys generated and stored');
        } else {
            // Load existing keys
            await this.loadKeysLocally();
            console.log('Existing keys loaded');
        }
    }
}

window.ChatEncryption = ChatEncryption;