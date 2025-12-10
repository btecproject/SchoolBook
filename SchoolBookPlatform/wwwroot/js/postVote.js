// postVote.js - Complete vote handling with container color update
class PostVote {
    constructor() {
        this.initialized = new Set();
        this.voteStatusCache = new Map();
        this.reportModal = null;
        this.init();
    }

    init() {
        this.cacheInitialVoteStatus();
        this.bindVoteEvents();
        this.bindReportEvents();
        this.setupMutationObserver();
        this.initializeReportModal();

        console.log('PostVote system initialized with report functionality');
    }

    cacheInitialVoteStatus() {
        document.querySelectorAll('.vote-container').forEach(container => {
            const postElement = container.closest('.post-card');
            if (!postElement) return;

            const postId = postElement.dataset.postId;
            if (!postId) return;

            const isUpvoted = container.classList.contains('vote-container-upvoted');
            const isDownvoted = container.classList.contains('vote-container-downvoted');

            this.voteStatusCache.set(postId, {
                isUpvoted,
                isDownvoted,
                lastUpdated: new Date().toISOString()
            });
        });
    }

    initializeReportModal() {
        const modalElement = document.getElementById('reportModal');
        if (modalElement) {
            this.reportModal = new bootstrap.Modal(modalElement);

            // Reset modal content when hidden
            modalElement.addEventListener('hidden.bs.modal', () => {
                const modalBody = document.getElementById('reportModalBody');
                if (modalBody) {
                    modalBody.innerHTML = `
                        <div class="text-center py-3">
                            <div class="spinner-border text-primary" role="status">
                                <span class="visually-hidden">Loading...</span>
                            </div>
                        </div>
                    `;
                }
            });
        }
    }

    setupMutationObserver() {
        const observer = new MutationObserver((mutations) => {
            mutations.forEach((mutation) => {
                if (mutation.addedNodes.length) {
                    mutation.addedNodes.forEach((node) => {
                        if (node.nodeType === 1) {
                            // Initialize vote containers
                            const newContainers = node.querySelectorAll ?
                                node.querySelectorAll('.vote-container') : [];

                            newContainers.forEach(container => {
                                const postElement = container.closest('.post-card');
                                if (postElement) {
                                    const postId = postElement.dataset.postId;
                                    if (postId) {
                                        this.cacheVoteStatusForContainer(container, postId);
                                        this.bindVoteEventsForContainer(container);
                                    }
                                }
                            });

                            // Initialize report buttons in new posts
                            const newReportButtons = node.querySelectorAll ?
                                node.querySelectorAll('[data-bs-target="#reportModal"]') : [];

                            newReportButtons.forEach(button => {
                                this.bindReportButton(button);
                            });
                        }
                    });
                }
            });
        });

        observer.observe(document.body, { childList: true, subtree: true });
    }

    cacheVoteStatusForContainer(container, postId) {
        const isUpvoted = container.classList.contains('vote-container-upvoted');
        const isDownvoted = container.classList.contains('vote-container-downvoted');

        this.voteStatusCache.set(postId, {
            isUpvoted,
            isDownvoted,
            lastUpdated: new Date().toISOString()
        });
    }

    bindVoteEvents() {
        document.querySelectorAll('.vote-up-btn, .vote-down-btn').forEach(button => {
            this.bindVoteButton(button);
        });
    }

    bindVoteEventsForContainer(container) {
        container.querySelectorAll('.vote-up-btn, .vote-down-btn').forEach(button => {
            this.bindVoteButton(button);
        });
    }

    bindVoteButton(button) {
        const postId = button.dataset.postId;
        const voteType = button.dataset.voteType;
        const buttonId = `${postId}-${voteType}`;

        if (!this.initialized.has(buttonId)) {
            this.initialized.add(buttonId);
            button.removeEventListener('click', this.handleVoteClick);
            button.addEventListener('click', (e) => this.handleVoteClick(e, button));
            button.title = voteType === 'true' ? 'Upvote' : 'Downvote';
        }
    }

    bindReportEvents() {
        // Bind report buttons
        document.querySelectorAll('[data-bs-target="#reportModal"]').forEach(button => {
            this.bindReportButton(button);
        });

        // Bind form submit event
        document.addEventListener('submit', async (e) => {
            if (e.target.classList.contains('report-form')) {
                e.preventDefault();
                await this.handleReportSubmit(e.target);
            }
        });
    }

    bindReportButton(button) {
        const buttonId = button.dataset.buttonId || Math.random().toString(36).substr(2, 9);

        if (!this.initialized.has(`report-${buttonId}`)) {
            this.initialized.add(`report-${buttonId}`);

            button.removeEventListener('click', this.handleReportButtonClick);
            button.addEventListener('click', (e) => this.handleReportButtonClick(e, button));
        }
    }

    handleReportButtonClick = (e, button) => {
        e.preventDefault();
        e.stopPropagation();

        const postId = button.closest('.post-card')?.dataset.postId;
        if (!postId) {
            // Try to get postId from data attribute
            postId = button.dataset.postId;
        }

        if (postId) {
            this.loadReportForm(postId);
        }
    }

    async loadReportForm(postId) {
        const modalBody = document.getElementById('reportModalBody');
        const modalLabel = document.getElementById('reportModalLabel');

        if (!modalBody) return;

        // Show loading
        modalBody.innerHTML = `
            <div class="text-center py-3">
                <div class="spinner-border text-primary" role="status">
                    <span class="visually-hidden">Loading...</span>
                </div>
            </div>
        `;

        try {
            // Load report form via AJAX
            const response = await fetch(`/PostReport/ReportForm/${postId}`);

            if (response.ok) {
                const html = await response.text();
                modalBody.innerHTML = html;

                // Update modal title
                if (modalLabel) {
                    modalLabel.textContent = 'Báo cáo bài đăng';
                }

                // Add animation to the form
                const form = modalBody.querySelector('.report-form');
                if (form) {
                    form.style.opacity = '0';
                    form.style.transform = 'translateY(10px)';
                    form.style.transition = 'all 0.3s ease';

                    setTimeout(() => {
                        form.style.opacity = '1';
                        form.style.transform = 'translateY(0)';
                    }, 10);
                }
            } else {
                modalBody.innerHTML = `
                    <div class="alert alert-danger">
                        <i class="fas fa-exclamation-circle me-2"></i>
                        Không thể tải form báo cáo. Vui lòng thử lại.
                    </div>
                `;
            }
        } catch (error) {
            console.error('Error loading report form:', error);
            modalBody.innerHTML = `
                <div class="alert alert-danger">
                    <i class="fas fa-exclamation-circle me-2"></i>
                    Lỗi kết nối mạng. Vui lòng kiểm tra kết nối và thử lại.
                </div>
            `;
        }
    }

    async handleReportSubmit(form) {
        const submitBtn = form.querySelector('button[type="submit"]');
        const originalText = submitBtn.innerHTML;

        // Show loading state
        submitBtn.innerHTML = `
            <span class="spinner-border spinner-border-sm me-2" role="status" aria-hidden="true"></span>
            Đang gửi...
        `;
        submitBtn.disabled = true;

        try {
            const formData = new FormData(form);
            const response = await fetch('/Post/Report', {
                method: 'POST',
                body: formData
            });

            if (response.ok) {
                // Success
                this.showToast('Báo cáo đã được gửi thành công!', 'success');

                // Close modal after delay
                setTimeout(() => {
                    if (this.reportModal) {
                        this.reportModal.hide();
                    }
                }, 1500);

                // Reset form
                form.reset();
            } else {
                // Server error
                const data = await response.json();
                this.showToast(data.message || 'Có lỗi xảy ra khi gửi báo cáo.', 'danger');
            }
        } catch (error) {
            console.error('Error submitting report:', error);
            this.showToast('Lỗi kết nối mạng. Vui lòng thử lại.', 'danger');
        } finally {
            // Restore button
            submitBtn.innerHTML = originalText;
            submitBtn.disabled = false;
        }
    }

    handleVoteClick = async (e, button) => {
        e.preventDefault();
        e.stopPropagation();

        const postId = button.dataset.postId;
        const isUpvote = button.dataset.voteType === 'true';

        // Visual feedback
        this.animateVoteClick(button);

        try {
            const response = await fetch('/Post/Vote', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/x-www-form-urlencoded',
                },
                body: `postId=${postId}&isUpvote=${isUpvote}`
            });

            if (!response.ok) {
                throw new Error(`HTTP error: ${response.status}`);
            }

            const data = await response.json();

            if (data.success) {
                this.updateVoteUI(postId, data, isUpvote);
                this.showToast('Đã cập nhật vote thành công!', 'success');
            } else {
                this.showToast(data.message || 'Vote thất bại', 'danger');
            }
        } catch (error) {
            console.error('Vote error:', error);
            this.showToast('Lỗi kết nối mạng. Vui lòng thử lại.', 'danger');
        }
    }

    animateVoteClick(button) {
        const ripple = document.createElement('span');
        ripple.className = 'vote-ripple';
        button.appendChild(ripple);

        button.classList.add('voting');

        setTimeout(() => {
            button.classList.remove('voting');
            if (ripple.parentNode === button) {
                button.removeChild(ripple);
            }
        }, 300);
    }

    updateVoteUI(postId, data, isUpvote) {
        const postElement = document.querySelector(`.post-card[data-post-id="${postId}"]`);
        if (!postElement) return;

        const container = postElement.querySelector('.vote-container');
        const upvoteBtn = postElement.querySelector('.vote-up-btn');
        const downvoteBtn = postElement.querySelector('.vote-down-btn');
        const scoreElement = postElement.querySelector(`.vote-score[data-post-id="${postId}"]`);

        if (!container || !upvoteBtn || !downvoteBtn || !scoreElement) return;

        // Determine new vote status
        let newIsUpvoted = false;
        let newIsDownvoted = false;

        if (data.userVoteStatus !== undefined) {
            if (data.userVoteStatus === true) {
                newIsUpvoted = true;
            } else if (data.userVoteStatus === false) {
                newIsDownvoted = true;
            }
        } else {
            const currentStatus = this.voteStatusCache.get(postId) || { isUpvoted: false, isDownvoted: false };

            if (isUpvote) {
                newIsUpvoted = !currentStatus.isUpvoted;
            } else {
                newIsDownvoted = !currentStatus.isDownvoted;
            }
        }

        // Update cache
        this.voteStatusCache.set(postId, {
            isUpvoted: newIsUpvoted,
            isDownvoted: newIsDownvoted,
            lastUpdated: new Date().toISOString()
        });

        // Update container color
        container.classList.remove('vote-container-upvoted', 'vote-container-downvoted');

        if (newIsUpvoted) {
            container.classList.add('vote-container-upvoted');
        } else if (newIsDownvoted) {
            container.classList.add('vote-container-downvoted');
        }

        // Update button states
        upvoteBtn.classList.remove('active');
        downvoteBtn.classList.remove('active');

        if (newIsUpvoted) {
            upvoteBtn.classList.add('active');
        } else if (newIsDownvoted) {
            downvoteBtn.classList.add('active');
        }

        // Update score
        this.updateScore(scoreElement, data);
    }

    updateScore(scoreElement, data) {
        let displayScore;

        if (data.displayScore !== undefined) {
            displayScore = data.displayScore;
        } else if (data.totalScore !== undefined) {
            displayScore = data.totalScore < 0 ? 0 : data.totalScore;
        } else if (data.upvoteCount !== undefined && data.downvoteCount !== undefined) {
            const totalScore = data.upvoteCount - data.downvoteCount;
            displayScore = totalScore < 0 ? 0 : totalScore;
        } else {
            const currentScore = parseInt(scoreElement.textContent) || 0;
            displayScore = currentScore;
        }

        // Add animation
        scoreElement.textContent = displayScore;
        scoreElement.classList.add('score-updated');

        setTimeout(() => {
            scoreElement.classList.remove('score-updated');
        }, 500);
    }

    showToast(message, type = 'info') {
        // Create toast container if not exists
        let toastContainer = document.getElementById('toast-container');
        if (!toastContainer) {
            toastContainer = document.createElement('div');
            toastContainer.id = 'toast-container';
            toastContainer.className = 'toast-container position-fixed bottom-0 end-0 p-3';
            document.body.appendChild(toastContainer);
        }

        // Create toast
        const toastId = 'toast-' + Date.now();
        const toast = document.createElement('div');
        toast.id = toastId;
        toast.className = `toast align-items-center text-bg-${type} border-0`;
        toast.setAttribute('role', 'alert');
        toast.setAttribute('aria-live', 'assertive');
        toast.setAttribute('aria-atomic', 'true');

        const iconClass = {
            'success': 'fa-check-circle',
            'danger': 'fa-exclamation-circle',
            'warning': 'fa-exclamation-triangle',
            'info': 'fa-info-circle'
        }[type] || 'fa-info-circle';

        toast.innerHTML = `
            <div class="d-flex align-items-center">
                <div class="toast-icon me-2">
                    <i class="fas ${iconClass}"></i>
                </div>
                <div class="toast-body flex-grow-1">
                    ${message}
                </div>
                <button type="button" class="btn-close btn-close-white me-2" data-bs-dismiss="toast"></button>
            </div>
        `;

        toastContainer.appendChild(toast);

        // Show toast
        const bsToast = new bootstrap.Toast(toast, {
            autohide: true,
            delay: 3000
        });
        bsToast.show();

        // Remove toast after hide
        toast.addEventListener('hidden.bs.toast', function() {
            toast.remove();
        });
    }

    // Public method to manually update vote status
    updatePostVoteStatus(postId, isUpvoted, isDownvoted) {
        const postElement = document.querySelector(`.post-card[data-post-id="${postId}"]`);
        if (!postElement) return;

        const container = postElement.querySelector('.vote-container');
        const upvoteBtn = postElement.querySelector('.vote-up-btn');
        const downvoteBtn = postElement.querySelector('.vote-down-btn');

        if (!container || !upvoteBtn || !downvoteBtn) return;

        container.classList.remove('vote-container-upvoted', 'vote-container-downvoted');

        if (isUpvoted) {
            container.classList.add('vote-container-upvoted');
        } else if (isDownvoted) {
            container.classList.add('vote-container-downvoted');
        }

        upvoteBtn.classList.remove('active');
        downvoteBtn.classList.remove('active');

        if (isUpvoted) {
            upvoteBtn.classList.add('active');
        } else if (isDownvoted) {
            downvoteBtn.classList.add('active');
        }

        this.voteStatusCache.set(postId, {
            isUpvoted,
            isDownvoted,
            lastUpdated: new Date().toISOString()
        });
    }
}

// Initialize when DOM is ready
document.addEventListener('DOMContentLoaded', () => {
    const voteContainers = document.querySelectorAll('.vote-container');
    const reportButtons = document.querySelectorAll('[data-bs-target="#reportModal"]');

    if (voteContainers.length > 0 || reportButtons.length > 0) {
        window.postVote = new PostVote();
        console.log(`Post system initialized for ${voteContainers.length} posts`);
    }
});

// Global helper function for loading report form (can be called from inline onclick)
if (typeof window !== 'undefined') {
    window.loadReportForm = function(postId) {
        if (window.postVote && window.postVote.loadReportForm) {
            window.postVote.loadReportForm(postId);
        } else {
            console.error('PostVote system not initialized');
        }
    };

    window.PostVote = PostVote;
}

// Tạo ID duy nhất cho mỗi nút report
document.querySelectorAll('[data-bs-target="#reportModal"]').forEach((button, index) => {
    button.dataset.buttonId = `report-btn-${index}`;
});

// Xử lý click vào nút report trong dropdown
document.querySelectorAll('.dropdown-item[data-bs-target="#reportModal"]').forEach(button => {
    button.addEventListener('click', function(e) {
        e.stopPropagation();
        const postId = this.closest('.post-card')?.dataset.postId;
        if (postId) {
            // Gọi hàm từ postVote instance
            if (window.postVote) {
                window.postVote.loadReportForm(postId);
            }
        }
    });
});

// reportModal.js - Xử lý modal báo cáo bài đăng
class ReportModal {
    constructor() {
        this.modal = null;
        this.modalBody = null;
        this.currentPostId = null;
        this.init();
    }

    init() {
        // Khởi tạo modal
        const modalElement = document.getElementById('reportModal');
        if (modalElement) {
            this.modal = new bootstrap.Modal(modalElement);
            this.modalBody = document.getElementById('reportModalBody');

            // Xử lý sự kiện khi modal đóng
            modalElement.addEventListener('hidden.bs.modal', () => {
                this.resetModal();
            });

            // Xử lý sự kiện khi modal hiển thị
            modalElement.addEventListener('show.bs.modal', () => {
                this.onModalShow();
            });
        }

        // Bind sự kiện cho tất cả nút báo cáo
        this.bindReportButtons();

        // Setup MutationObserver cho dynamic content
        this.setupMutationObserver();

        console.log('ReportModal initialized');
    }

    bindReportButtons() {
        // Bind cho nút báo cáo trong dropdown
        document.querySelectorAll('.dropdown-item.text-warning[data-bs-target="#reportModal"]').forEach(button => {
            button.addEventListener('click', (e) => {
                e.stopPropagation();
                const postId = this.getPostIdFromElement(button);
                if (postId) {
                    this.showReportForm(postId);
                }
            });
        });

        // Bind cho nút báo cáo trực tiếp
        document.querySelectorAll('[onclick*="loadReportForm"]').forEach(button => {
            button.addEventListener('click', (e) => {
                e.stopPropagation();
                const postId = this.getPostIdFromElement(button) ||
                    button.getAttribute('onclick')?.match(/'([^']+)'/)?.[1];
                if (postId) {
                    this.showReportForm(postId);
                }
            });
        });
    }

    setupMutationObserver() {
        const observer = new MutationObserver((mutations) => {
            mutations.forEach((mutation) => {
                if (mutation.addedNodes.length) {
                    mutation.addedNodes.forEach((node) => {
                        if (node.nodeType === 1) {
                            // Bind sự kiện cho nút báo cáo mới
                            const newReportButtons = node.querySelectorAll ?
                                node.querySelectorAll('[data-bs-target="#reportModal"], [onclick*="loadReportForm"]') : [];

                            newReportButtons.forEach(button => {
                                button.addEventListener('click', (e) => {
                                    e.stopPropagation();
                                    const postId = this.getPostIdFromElement(button) ||
                                        button.getAttribute('onclick')?.match(/'([^']+)'/)?.[1];
                                    if (postId) {
                                        this.showReportForm(postId);
                                    }
                                });
                            });
                        }
                    });
                }
            });
        });

        observer.observe(document.body, { childList: true, subtree: true });
    }

    getPostIdFromElement(element) {
        // Lấy postId từ các vị trí có thể
        const postCard = element.closest('.post-card');
        if (postCard) {
            return postCard.dataset.postId;
        }

        const card = element.closest('.card');
        if (card) {
            return card.dataset.postId ||
                card.querySelector('.post-card')?.dataset.postId;
        }

        return element.dataset.postId || null;
    }

    async showReportForm(postId) {
        this.currentPostId = postId;

        // Hiển thị modal
        if (this.modal) {
            this.modal.show();
        }

        // Hiển thị loading
        this.showLoading();

        try {
            // Load form báo cáo
            const response = await fetch(`/PostReport/ReportForm/${postId}`);

            if (!response.ok) {
                throw new Error(`HTTP error: ${response.status}`);
            }

            const html = await response.text();

            // Nếu response là JSON error
            if (html.trim().startsWith('{')) {
                const data = JSON.parse(html);
                this.showError(data.message || 'Không thể tải form báo cáo.');
                return;
            }

            // Cập nhật modal với form
            this.modalBody.innerHTML = html;

            // Thêm animation
            const formContainer = this.modalBody.querySelector('.report-form-container');
            if (formContainer) {
                formContainer.style.opacity = '0';
                formContainer.style.transform = 'translateY(10px)';
                formContainer.style.transition = 'all 0.3s ease';

                setTimeout(() => {
                    formContainer.style.opacity = '1';
                    formContainer.style.transform = 'translateY(0)';
                }, 50);
            }

        } catch (error) {
            console.error('Error loading report form:', error);
            this.showError('Lỗi kết nối mạng. Vui lòng thử lại.');
        }
    }

    showLoading() {
        if (this.modalBody) {
            this.modalBody.innerHTML = `
                <div class="text-center py-5">
                    <div class="spinner-border text-primary" style="width: 3rem; height: 3rem;" role="status">
                        <span class="visually-hidden">Loading...</span>
                    </div>
                    <p class="mt-3 text-muted">Đang tải form báo cáo...</p>
                </div>
            `;
        }
    }

    showError(message) {
        if (this.modalBody) {
            this.modalBody.innerHTML = `
                <div class="text-center py-5">
                    <div class="mb-3">
                        <i class="fas fa-exclamation-triangle text-danger fa-4x"></i>
                    </div>
                    <h5 class="text-danger">Lỗi!</h5>
                    <p class="text-muted">${message}</p>
                    <div class="mt-4">
                        <button class="btn btn-secondary me-2" data-bs-dismiss="modal">Đóng</button>
                        <button class="btn btn-primary" onclick="reportModal.retryLoadForm()">Thử lại</button>
                    </div>
                </div>
            `;
        }
    }

    retryLoadForm() {
        if (this.currentPostId) {
            this.showReportForm(this.currentPostId);
        }
    }

    resetModal() {
        if (this.modalBody) {
            this.modalBody.innerHTML = `
                <div class="text-center py-5">
                    <div class="spinner-border text-primary" role="status">
                        <span class="visually-hidden">Loading...</span>
                    </div>
                </div>
            `;
        }
        this.currentPostId = null;
    }

    onModalShow() {
        // Có thể thêm các xử lý khi modal hiển thị
        console.log('Report modal shown for post:', this.currentPostId);
    }
}

// Khởi tạo khi DOM ready
document.addEventListener('DOMContentLoaded', () => {
    window.reportModal = new ReportModal();
    console.log('ReportModal system ready');
});