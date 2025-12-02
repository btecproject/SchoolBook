class ChatEncryption {
    constructor() {
        this.keyPair = null;
        this.threadKeys = new Map();
        this.publicKeys = new Map();
        this._isInitialized = false;  // ← THÊM FLAG
    }

    // ===================================================
    // CHECK IF READY
    // ===================================================

    /**
     * Check if encryption is ready to use
     * @returns {boolean}
     */
    isReady() {
        return this._isInitialized && this.keyPair !== null;
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
    // 3. FETCH PUBLIC KEYS FOR THREAD
    // ===================================================

    /**
     * Fetch public keys of all users in a thread
     * @param {number} threadId
     */
    async fetchPublicKeysForThread(threadId) {
        try {
            console.log(` Fetching public keys for thread ${threadId}...`);

            const response = await fetch(`/api/chat/thread/${threadId}/public-keys`);

            if (!response.ok) {
                throw new Error(`Failed to fetch public keys: ${response.status}`);
            }

            const keys = await response.json();

            console.log(` Received ${keys.length} public keys from server`);
            console.log(` Keys data:`, keys);

            //  CRITICAL: Also add our own public key to the map!
            const currentUsername = typeof currentUser !== 'undefined' ? currentUser : null;

            if (currentUsername && this.keyPair && this.keyPair.publicKey) {
                // Add our own public key
                this.publicKeys.set(currentUsername, this.keyPair.publicKey);
                console.log(` Added own public key for: ${currentUsername}`);
            }

            // Import and store each public key from other users
            for (const keyData of keys) {
                try {
                    const publicKey = await this.importPublicKey(keyData.publicKey);

                    // CRITICAL: Store with username as key
                    const username = keyData.userId;
                    this.publicKeys.set(username, publicKey);

                    console.log(` Imported public key for user: ${username}`);
                    console.log(`   Key stored in Map with key: "${username}"`);
                } catch (err) {
                    console.error(` Failed to import key for ${keyData.userId}:`, err);
                }
            }

            console.log(` Total keys stored: ${this.publicKeys.size}`);
            console.log(` Map keys:`, Array.from(this.publicKeys.keys()));
        } catch (error) {
            console.error(' Failed to fetch public keys:', error);
            throw error;
        }
    }

    /**
     * Upload public key to server
     */
    async uploadPublicKey() {
        try {
            if (!this.keyPair || !this.keyPair.publicKey) {
                throw new Error('No public key to upload');
            }

            const publicKeyBase64 = await this.exportPublicKey(this.keyPair.publicKey);

            console.log(' Uploading public key to server...');

            const response = await fetch('/api/chat/upload-public-key', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify({
                    publicKey: publicKeyBase64
                })
            });

            if (!response.ok) {
                const error = await response.json();
                throw new Error(error.error || 'Failed to upload public key');
            }

            const result = await response.json();
            console.log(' Public key uploaded:', result.message);
        } catch (error) {
            console.error(' Failed to upload public key:', error);
            throw error;
        }
    }

    // ===================================================
    // 4. MESSAGE ENCRYPTION
    // ===================================================

    /**
     * MÃ HÓA TIN NHẮN CHO NHIỀU NGƯỜI (E2EE nhóm)
     * @param {string} message - Nội dung tin nhắn
     * @param {string[]} recipients - Danh sách username của người nhận (trừ mình)
     * @returns {Promise<{encryptedContent: string, iv: string, encryptedKey: string}>}
     */
    async encryptMessage(message, recipients) {
        try {
            console.log(' Mã hóa tin nhắn cho', recipients.length, 'người nhận');
            console.log(' Recipients:', recipients);

            if (!this.isReady()) {
                throw new Error('Encryption not initialized');
            }

            // 1. Tạo AES key ngẫu nhiên cho tin nhắn này
            const aesKey = await this.generateAESKey();
            const iv = window.crypto.getRandomValues(new Uint8Array(12));

            // 2. Mã hóa nội dung bằng AES
            const encoder = new TextEncoder();
            const encryptedContent = await window.crypto.subtle.encrypt(
                { name: "AES-GCM", iv: iv },
                aesKey,
                encoder.encode(message)
            );

            // 3. Export AES key để mã hóa bằng RSA
            const exportedAesKey = await window.crypto.subtle.exportKey("raw", aesKey);

            // 4. Mã hóa AES key bằng public key của TỪNG người nhận + chính mình
            const encryptedKeys = {};

            // CRITICAL: Get current username
            const currentUsername = typeof currentUser !== 'undefined' ? currentUser : null;

            if (!currentUsername) {
                throw new Error('Current user is not defined');
            }

            console.log(` Current user: ${currentUsername}`);

            // Luôn mã hóa cho chính mình trước
            const myEncryptedKey = await this.encryptKeyWithPublicKey(
                exportedAesKey,
                this.keyPair.publicKey
            );
            encryptedKeys[currentUsername] = btoa(String.fromCharCode(...new Uint8Array(myEncryptedKey)));
            console.log(`Encrypted key for self: ${currentUsername}`);

            // Mã hóa cho từng người nhận (nếu có public key)
            console.log(` Public keys available:`, Array.from(this.publicKeys.keys()));

            for (const username of recipients) {
                console.log(` Looking for public key of: "${username}"`);

                if (this.publicKeys.has(username)) {
                    const pubKey = this.publicKeys.get(username);
                    const encKey = await this.encryptKeyWithPublicKey(exportedAesKey, pubKey);
                    encryptedKeys[username] = btoa(String.fromCharCode(...new Uint8Array(encKey)));
                    console.log(` Encrypted key for: ${username}`);
                } else {
                    console.warn(` No public key found for: "${username}"`);
                    console.warn(`   Available keys:`, Array.from(this.publicKeys.keys()));
                }
            }

            console.log(` Message encrypted for ${Object.keys(encryptedKeys).length} users`);
            console.log(` Encrypted keys created for:`, Object.keys(encryptedKeys));

            // 5. Trả về dữ liệu
            return {
                encryptedContent: btoa(String.fromCharCode(...new Uint8Array(encryptedContent))),
                iv: btoa(String.fromCharCode(...iv)),
                encryptedKey: JSON.stringify(encryptedKeys)
            };

        } catch (error) {
            console.error('Mã hóa tin nhắn thất bại:', error);
            console.error('   Error name:', error.name);
            console.error('   Error message:', error.message);
            throw error;
        }
    }

    /**
     * Decrypt message content
     * @param {string} encryptedContentBase64
     * @param {string} ivBase64
     * @param {string} encryptedKeyJson
     * @returns {Promise<string>} - Decrypted plain text
     */
    async decryptMessage(encryptedContentBase64, ivBase64, encryptedKeyJson) {
        try {
            console.log(' Giải mã tin nhắn...');
            console.log(`   Current user: ${typeof currentUser !== 'undefined' ? currentUser : 'UNDEFINED'}`);

            if (!this.isReady()) {
                throw new Error('Encryption not initialized');
            }

            const encryptedContent = Uint8Array.from(atob(encryptedContentBase64), c => c.charCodeAt(0));
            const iv = Uint8Array.from(atob(ivBase64), c => c.charCodeAt(0));
            const keyMap = JSON.parse(encryptedKeyJson);

            console.log(` Encrypted keys available for users:`, Object.keys(keyMap));

            // CRITICAL: Find current user's key
            const currentUsername = typeof currentUser !== 'undefined' ? currentUser : null;

            if (!currentUsername) {
                throw new Error('Current user is not defined');
            }

            if (!keyMap[currentUsername]) {
                console.error(` No encrypted key found for current user: "${currentUsername}"`);
                console.error(`   Available keys:`, Object.keys(keyMap));
                throw new Error(`Không có key cho user hiện tại: ${currentUsername}`);
            }

            console.log(` Found encrypted key for ${currentUsername}`);

            const myEncryptedKey = Uint8Array.from(atob(keyMap[currentUsername]), c => c.charCodeAt(0));

            // Giải mã AES key bằng private key của mình
            console.log(` Decrypting AES key with RSA private key...`);
            const aesKeyRaw = await window.crypto.subtle.decrypt(
                { name: "RSA-OAEP" },
                this.keyPair.privateKey,
                myEncryptedKey
            );

            const aesKey = await window.crypto.subtle.importKey(
                "raw",
                aesKeyRaw,
                "AES-GCM",
                true,
                ["decrypt"]
            );

            console.log(` Decrypting message content with AES key...`);

            // Giải mã nội dung
            const decrypted = await window.crypto.subtle.decrypt(
                { name: "AES-GCM", iv: iv },
                aesKey,
                encryptedContent
            );

            const plainText = new TextDecoder().decode(decrypted);
            console.log(` Giải mã thành công: "${plainText}"`);
            return plainText;

        } catch (error) {
            console.error(' Giải mã thất bại:', error);
            console.error('   Error name:', error.name);
            console.error('   Error message:', error.message);
            return "[ Encrypted Message]";
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
    // INDEXEDDB STORAGE FOR PRIVATE KEY
    // ===================================================

    /**
     * Open IndexedDB for storing private key
     */
    async openDB() {
        return new Promise((resolve, reject) => {
            const request = indexedDB.open('ChatEncryptionDB', 1);

            request.onerror = () => reject(request.error);
            request.onsuccess = () => resolve(request.result);

            request.onupgradeneeded = (event) => {
                const db = event.target.result;
                if (!db.objectStoreNames.contains('keys')) {
                    db.createObjectStore('keys');
                }
            };
        });
    }

    /**
     * Store keypair in IndexedDB
     */
    async storeKeyPairInDB() {
        try {
            const db = await this.openDB();

            // Export keys
            const publicKeyData = await window.crypto.subtle.exportKey('spki', this.keyPair.publicKey);
            const privateKeyData = await window.crypto.subtle.exportKey('pkcs8', this.keyPair.privateKey);

            // Store as base64
            const keyData = {
                publicKey: btoa(String.fromCharCode(...new Uint8Array(publicKeyData))),
                privateKey: btoa(String.fromCharCode(...new Uint8Array(privateKeyData))),
                createdAt: Date.now()
            };

            const transaction = db.transaction(['keys'], 'readwrite');
            const store = transaction.objectStore('keys');
            store.put(keyData, 'userKeyPair');

            await new Promise((resolve, reject) => {
                transaction.oncomplete = resolve;
                transaction.onerror = () => reject(transaction.error);
            });

            console.log(' Keypair stored in IndexedDB');
            db.close();
        } catch (error) {
            console.error(' Failed to store keypair:', error);
            throw error;
        }
    }

    /**
     * Load keypair from IndexedDB
     */
    async loadKeyPairFromDB() {
        try {
            const db = await this.openDB();

            const transaction = db.transaction(['keys'], 'readonly');
            const store = transaction.objectStore('keys');
            const request = store.get('userKeyPair');

            const keyData = await new Promise((resolve, reject) => {
                request.onsuccess = () => resolve(request.result);
                request.onerror = () => reject(request.error);
            });

            db.close();

            if (!keyData) {
                console.log(' No keypair found in IndexedDB');
                return null;
            }

            // Import keys
            const publicKeyData = Uint8Array.from(atob(keyData.publicKey), c => c.charCodeAt(0));
            const privateKeyData = Uint8Array.from(atob(keyData.privateKey), c => c.charCodeAt(0));

            const publicKey = await window.crypto.subtle.importKey(
                'spki',
                publicKeyData,
                { name: 'RSA-OAEP', hash: 'SHA-256' },
                true,
                ['encrypt']
            );

            const privateKey = await window.crypto.subtle.importKey(
                'pkcs8',
                privateKeyData,
                { name: 'RSA-OAEP', hash: 'SHA-256' },
                true,
                ['decrypt']
            );

            this.keyPair = { publicKey, privateKey };

            console.log(' Keypair loaded from IndexedDB');
            console.log('   Created at:', new Date(keyData.createdAt).toLocaleString());

            return this.keyPair;
        } catch (error) {
            console.error(' Failed to load keypair:', error);
            return null;
        }
    }

    /**
     * Clear stored keypair (for logout/reset)
     */
    async clearStoredKeyPair() {
        try {
            const db = await this.openDB();
            const transaction = db.transaction(['keys'], 'readwrite');
            const store = transaction.objectStore('keys');
            store.delete('userKeyPair');

            await new Promise((resolve, reject) => {
                transaction.oncomplete = resolve;
                transaction.onerror = () => reject(transaction.error);
            });

            console.log(' Keypair cleared from IndexedDB');
            db.close();
        } catch (error) {
            console.error(' Failed to clear keypair:', error);
        }
    }

    /**
     * Store keys in browser (localStorage)
     * WARNING: For demo only, use secure storage in production
     */
    async storeKeysLocally() {
        if (!this.keyPair) {
            throw new Error('No key pair to store');
        }

        const publicKeyBase64 = await this.exportPublicKey(this.keyPair.publicKey);
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

            // Recreate keyPair with loaded public key
            // Note: Private key should be loaded separately if stored
            this.keyPair = {
                publicKey: publicKey,
                privateKey: null // Will be set during full initialization
            };

            console.log(' Public key loaded from storage');
            return true;
        }

        return false;
    }

    /**
     * Initialize encryption for user
     */
    async initialize() {
        try {
            console.log(' Initializing chat encryption...');


            const existingKeyPair = await this.loadKeyPairFromDB();

            if (existingKeyPair) {
                console.log(' Using existing keypair from IndexedDB');
                
                const publicKeyBase64 = await this.exportPublicKey(this.keyPair.publicKey);
                localStorage.setItem('chat_public_key', publicKeyBase64);

                // Verify public key on server matches
                try {
                    const serverKeyCheck = await fetch('/api/chat/my-public-key');
                    if (serverKeyCheck.ok) {
                        const serverKey = await serverKeyCheck.json();
                        if (serverKey.publicKey !== publicKeyBase64) {
                            console.warn(' Server public key mismatch, uploading new key...');
                            await this.uploadPublicKey();
                        } else {
                            console.log(' Server public key matches local key');
                        }
                    } else {
                        // No key on server, upload
                        await this.uploadPublicKey();
                    }
                } catch (err) {
                    console.warn(' Could not verify server key, uploading...', err);
                    await this.uploadPublicKey();
                }
            } else {
                //  STEP 2: Generate new keypair if none exists
                console.log(' No existing keypair, generating new...');

                await this.generateKeyPair();

                // Store in IndexedDB for persistence
                await this.storeKeyPairInDB();

                // Store public key in localStorage
                await this.storeKeysLocally();
                
                await this.uploadPublicKey();

                console.log(' New keypair generated and stored');
            }

            // Mark as initialized
            this._isInitialized = true;
            console.log('Encryption initialization complete');

        } catch (error) {
            console.error(' Encryption initialization failed:', error);
            this._isInitialized = false;
            throw error;
        }
    }
    /**
     * Reset encryption (clear all keys and reinitialize)
     * Useful when encryption is broken or for testing
     */
    async resetEncryption() {
        try {
            console.log(' Resetting encryption...');

            // Clear IndexedDB
            await this.clearStoredKeyPair();

            // Clear localStorage
            localStorage.removeItem('chat_public_key');

            // Clear server key
            try {
                await fetch('/api/chat/my-encryption-keys', { method: 'DELETE' });
                console.log(' Server keys deleted');
            } catch (err) {
                console.warn('⚠ Could not delete server keys:', err);
            }

            // Reset state
            this.keyPair = null;
            this.publicKeys.clear();
            this._isInitialized = false;

            console.log(' Encryption reset complete');
            console.log(' Reload page to reinitialize encryption');

        } catch (error) {
            console.error(' Failed to reset encryption:', error);
            throw error;
        }
    }
}

// Export to window
window.ChatEncryption = ChatEncryption;

//  Helper function to reset encryption (for debugging)
window.resetChatEncryption = async function() {
    if (window.chatEncryption) {
        await window.chatEncryption.resetEncryption();
        alert(' Encryption reset! Reload page to reinitialize.');
        location.reload();
    } else {
        alert(' No encryption instance found');
    }
};