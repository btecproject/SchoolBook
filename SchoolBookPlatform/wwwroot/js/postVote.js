// postVote.js - Complete vote handling with container color update
class PostVote {
    constructor() {
        this.initialized = new Set();
        this.voteStatusCache = new Map(); // Cache: postId -> { isUpvoted, isDownvoted }
        this.init();
    }

    init() {
        // Cache initial vote status from server-rendered HTML
        this.cacheInitialVoteStatus();

        // Bind vote events
        this.bindVoteEvents();

        // Set up observer for dynamically loaded content
        this.setupMutationObserver();

        console.log('PostVote system initialized');
    }

    cacheInitialVoteStatus() {
        // Cache vote status from existing posts
        document.querySelectorAll('.vote-container').forEach(container => {
            const postElement = container.closest('.post-card');
            if (!postElement) return;

            const postId = postElement.dataset.postId;
            if (!postId) return;

            // Extract vote status from CSS classes
            const isUpvoted = container.classList.contains('vote-container-upvoted');
            const isDownvoted = container.classList.contains('vote-container-downvoted');

            this.voteStatusCache.set(postId, {
                isUpvoted,
                isDownvoted,
                lastUpdated: new Date().toISOString()
            });
        });
    }

    setupMutationObserver() {
        const observer = new MutationObserver((mutations) => {
            mutations.forEach((mutation) => {
                if (mutation.addedNodes.length) {
                    mutation.addedNodes.forEach((node) => {
                        if (node.nodeType === 1) { // Element node
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

            // Remove old listener and add new one
            button.removeEventListener('click', this.handleVoteClick);
            button.addEventListener('click', (e) => this.handleVoteClick(e, button));

            // Add tooltip
            button.title = voteType === 'true' ? 'Upvote' : 'Downvote';
        }
    }

    handleVoteClick = async (e, button) => {
        e.preventDefault();
        e.stopPropagation();

        const postId = button.dataset.postId;
        const isUpvote = button.dataset.voteType === 'true';

        console.log(`Vote click: post ${postId}, isUpvote: ${isUpvote}`);

        // Get current vote status
        const currentStatus = this.voteStatusCache.get(postId) || { isUpvoted: false, isDownvoted: false };

        // Visual feedback
        this.animateVoteClick(button);

        try {
            // Send vote request to server
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
            console.log('Vote response:', data);

            if (data.success) {
                // Update UI with server response
                this.updateVoteUI(postId, data, isUpvote);
            } else {
                alert(data.message || 'Vote failed');
            }
        } catch (error) {
            console.error('Vote error:', error);
            alert('Network error. Please try again.');
        }
    }

    animateVoteClick(button) {
        // Add ripple effect
        const ripple = document.createElement('span');
        ripple.className = 'vote-ripple';
        button.appendChild(ripple);

        // Button animation
        button.classList.add('voting');

        // Clean up
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

        // Determine new vote status based on server response or toggle logic
        let newIsUpvoted = false;
        let newIsDownvoted = false;

        if (data.userVoteStatus !== undefined) {
            // Use server response if available
            if (data.userVoteStatus === true) {
                newIsUpvoted = true;
                newIsDownvoted = false;
            } else if (data.userVoteStatus === false) {
                newIsUpvoted = false;
                newIsDownvoted = true;
            } else {
                newIsUpvoted = false;
                newIsDownvoted = false;
            }
        } else {
            // Fallback: toggle logic
            const currentStatus = this.voteStatusCache.get(postId) || { isUpvoted: false, isDownvoted: false };

            if (isUpvote) {
                // Toggle upvote
                newIsUpvoted = !currentStatus.isUpvoted;
                newIsDownvoted = false;
            } else {
                // Toggle downvote
                newIsDownvoted = !currentStatus.isDownvoted;
                newIsUpvoted = false;
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
        // Calculate display score
        let displayScore;

        if (data.displayScore !== undefined) {
            displayScore = data.displayScore;
        } else if (data.totalScore !== undefined) {
            displayScore = data.totalScore < 0 ? 0 : data.totalScore;
        } else if (data.upvoteCount !== undefined && data.downvoteCount !== undefined) {
            const totalScore = data.upvoteCount - data.downvoteCount;
            displayScore = totalScore < 0 ? 0 : totalScore;
        } else {
            // Get current score and update based on vote
            const currentScore = parseInt(scoreElement.textContent) || 0;
            displayScore = currentScore;
        }

        // Update score display with animation
        scoreElement.textContent = displayScore;
        scoreElement.classList.add('score-updated');

        setTimeout(() => {
            scoreElement.classList.remove('score-updated');
        }, 500);
    }

    // Public method to manually update vote status (can be called from outside)
    updatePostVoteStatus(postId, isUpvoted, isDownvoted) {
        const postElement = document.querySelector(`.post-card[data-post-id="${postId}"]`);
        if (!postElement) return;

        const container = postElement.querySelector('.vote-container');
        const upvoteBtn = postElement.querySelector('.vote-up-btn');
        const downvoteBtn = postElement.querySelector('.vote-down-btn');

        if (!container || !upvoteBtn || !downvoteBtn) return;

        // Update container
        container.classList.remove('vote-container-upvoted', 'vote-container-downvoted');

        if (isUpvoted) {
            container.classList.add('vote-container-upvoted');
        } else if (isDownvoted) {
            container.classList.add('vote-container-downvoted');
        }

        // Update buttons
        upvoteBtn.classList.remove('active');
        downvoteBtn.classList.remove('active');

        if (isUpvoted) {
            upvoteBtn.classList.add('active');
        } else if (isDownvoted) {
            downvoteBtn.classList.add('active');
        }

        // Update cache
        this.voteStatusCache.set(postId, {
            isUpvoted,
            isDownvoted,
            lastUpdated: new Date().toISOString()
        });
    }
}

// Initialize when DOM is ready
document.addEventListener('DOMContentLoaded', () => {
    // Check if vote system should be initialized
    const voteContainers = document.querySelectorAll('.vote-container');

    if (voteContainers.length > 0) {
        window.postVote = new PostVote();
        console.log(`Vote system initialized for ${voteContainers.length} posts`);
    }
});

// Export for global access
if (typeof window !== 'undefined') {
    window.PostVote = PostVote;
}